# AutoAutoMapper

One of the biggest headaches when using [AutoMapper](https://github.com/AutoMapper/AutoMapper) is making sure to bootstrap all of your `CreateMap<TSource, TDestination>()` maps for it to work properly. One alternative is to go with [ValueInjecter, a convention-based mapper](http://valueinjecter.codeplex.com/) that doesn't require map definitions. I've also seen plenty of code that calls `AutoMapper.Mapper.CreateMap` in static controller constructors. Recently I was chatting with a web architect who had the opinion that AutoMapper is evil, and he sent me an `AutoMapperHelper` class he uses. I replied with my approach, and discovered he had never used the `AutoMapper.Profile` class.

## AutoMapper.Profile

Here is how it works. You just create a class that inherits from `AutoMapper.Profile`, override the void `Configure()` method, and call `this.CreateMap<TSource, TDestination>()`:

    public class MyCustomWidgetModelProfile : Profile
    {
        protected override void Configure()
        {
            this.CreateMap<WidgetEntity, WidgetViewModel>()
                // do all of your custom resolving here
                .ForMember(d => d.ViewOnlyProp, o => o.Ignore())
                .ForMember(d => d.CustomProp, o => o.ResolveUsing(s =>
                    new CustomType { s.Prop1, s.Prop2 }))
            ;
        }
    }

Then, instead of calling the static `AutoMapper.Mapper.CreateMap` method, all you have to do is add an instance of this `Profile` class to the `AutoMapper.Mapper.Configuration`:

    protected void Application_Start()
    {
        // among other things,
        AutoMapper.Mapper.Configuration.AddProfile<MyCustomWidgetModelProfile>();
    }

This does not get rid of the headache, since you still need to invoke `AddProfile` once for each `CreateMap` that needs to be bootstrapped. However with a base class to define all of our `CreateMap` code, it is much easier to use reflection and scan to find all of that code.

## AutoAutoMapper.AutoProfiler

To get rid of the headache, install the [AutoAutoMapper nuget package](http://nuget.org/packages/AutoAutoMapper) in your app. You can register all of your `AutoMapper.Profile` implementations with this single line of code:

    AutoAutoMapper.AutoProfiler.RegisterProfiles();

By default, this will scan the assembly where it is called from. For small projects with only one assembly, this is all you need. However if you have `AutoMapper.Profile` classes defined in a different assembly, or spread across multiple assemblies, you can use the `RegisterProfiles()` overloads that take `Assembly` arguments. One overlaod allows you to pass each assembly as a separate argument:

    AutoAutoMapper.AutoProfiler.RegisterProfiles(
        Assembly.GetAssembly(typeof(SomeProfileClassInAssemblyA)),
        Assembly.GetAssembly(typeof(SomeProfileClassInAssemblyB)),
        ...
        Assembly.GetAssembly(typeof(SomeProfileClassInAssemblyN))
    );

If you prefer to have all of your `Assembly` instances stacked up before invoking `RegisterProfiles`, there is another overload that takes only a single parameter:

    IEnumerable<Assembly> assemblies = new[]
        {
            Assembly.GetAssembly(typeof(SomeProfileClassInAssemblyA)),
            Assembly.GetAssembly(typeof(SomeProfileClassInAssemblyB)),
            ...
            Assembly.GetAssembly(typeof(SomeProfileClassInAssemblyN)),
        };
    AutoAutoMapper.AutoProfiler.RegisterProfiles(assemblies);

## App_Start/AutoMapperConfig.cs

The ASP.NET team has also recognized the need for better organization of bootstrap code, and adopted a new `App_Start` folder which you can see in action by creating a new default MVC4 project. With this approach, you have separate classes that deal with separate concerns in the `App_Start` folder. For example, the default template has a `BundleConfig` class, a `FilterConfig` class, a `RouteConfig` class, etc. Static methods on these classes are invoked during `Application_Start` to avoid cluttering the `Global.asax`:

    protected void Application_Start()
    {
        AreaRegistration.RegisterAllAreas();

        FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
        RouteConfig.RegisterRoutes(RouteTable.Routes);
        BundleConfig.RegisterBundles(BundleTable.Bundles);
    }

By default when you install the [AutoAutoMapper nuget package](http://nuget.org/packages/AutoAutoMapper), it will add a new `AutoMapperConfig.cs` class to the `App_Start` folder of your project. **It is your responsibility to wire this up by placing the following line in your Global.asax file's Application_Start method:**

    protected void Application_Start()
    {
        AreaRegistration.RegisterAllAreas();

        FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
        RouteConfig.RegisterRoutes(RouteTable.Routes);
        BundleConfig.RegisterBundles(BundleTable.Bundles);

        // the nuget package does not do this for you
        AutoMapperConfig.RegisterProfiles();
    }

You can then dive into the `AutoMapperConfig` class and tell it where to look for your `AutoMapper.Profile` implementations:

    public static class AutoMapperConfig
    {
        public static void RegisterProfiles()
        {
            AutoProfiler.RegisterProfiles(
                // put your arguments here, if you have any
            );
        }
    }


As soon as you can get your app to invoke `AutoAutoMapper.AutoProfiler.RegisterProfiles()`, it will scan your assembly(ies) for `AutoMapper.Profile` implementations and automatically add them to your `AutoMapper.Mapper.Configuration`. See [the source code](https://github.com/danludwig/AutoAutoMapper/blob/master/AutoAutoMapper/AutoProfiler.cs) for more information.

## Code Organization Patterns

You are now free to organize your `AutoMapper.Profile` implementations however you see fit. What I like to do is define them in the same files as the destination types. For example:

    // MyCustomViewModel.cs
    public class MyCustomViewModel
    {
        public string Prop1 { get; set; }
        public int Prop2 { get; set; }
    }

    // Also in MyCustomViewModel.cs
    public class MyCustomViewModelProfile : AutoMapper.Profile
    {
        protected override void Configure()
        {
            this.CreateMap<MyCustomEntity, MyCustomViewModel>()
                .ForMember(d => d.Prop2, o => o.Ignore())
            ;
        }
    }

This keeps the AutoMapper configuration close to the affected type. Sometimes, you may have multiple mappings for a model. In a CQRS system, you could have an `EditModel` that is hydrated by an entity, but can also be converted into a `Command` object to send for domain processing. For cases like these, I just wrap all of the `AutoMapper.Profile` implementations in single static type:

    public class MyCustomEditModel
    {
        public string Prop1 { get; set; }
        public int Prop2 { get; set; }
    }

    public static class MyCustomEditModelProfiler
    {
        public class EntityToModelProfile : AutoMapper.Profile
        {
            protected override void Configure()
            {
                this.CreateMap<MyCustomEntity, MyCustomEditModel();
            }
        }

        public class ModelToCommandProfile : AutoMapper.Profile
        {
            protected override void Configure()
            {
                this.CreateMap<MyCustomEditModel, MyCustomCommandDto();
            }
        }
    }

AutoAutoMapper's `AutoProfiler.RegisterProfiles()` will still discover these implementations, even though they are nested within an enclosing class.

