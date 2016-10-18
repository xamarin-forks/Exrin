﻿namespace Exrin.Framework
{
    using Abstraction;
    using Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    public class ViewService : IViewService
    {
        private readonly IInjectionProxy _injection = null;

        public ViewService(IInjectionProxy injection)
        {
            _injection = injection;
        }

        private readonly IDictionary<Type, TypeDefinition> _viewsByType = new Dictionary<Type, TypeDefinition>();

        private object GetBindingContext(Type viewType)
        {
            var viewModelType = _viewsByType[viewType];

            ConstructorInfo constructor = null;

            var parameters = new object[] { };

            constructor = viewModelType.Type.GetTypeInfo()
                   .DeclaredConstructors
                   .FirstOrDefault(c => !c.GetParameters().Any());

            if (constructor == null)
            {
                constructor = viewModelType.Type.GetTypeInfo()
                   .DeclaredConstructors.First();

                var parameterList = new List<object>();

                foreach (var param in constructor.GetParameters())
                    parameterList.Add(_injection.Get(param.ParameterType));

                parameters = parameterList.ToArray();
            }

            if (constructor == null)
                throw new InvalidOperationException(
                    $"No suitable constructor found for ViewModel {viewModelType.ToString()}");

            return constructor.Invoke(parameters);
        }

        public Task<IView> Build(Type viewType)
        {
            ConstructorInfo constructor = null;
            object[] parameters = null;

            constructor = viewType.GetTypeInfo()
                .DeclaredConstructors
                .FirstOrDefault(c => !c.GetParameters().Any());

            parameters = new object[] { };

            if (constructor == null)
                throw new InvalidOperationException(
                    $"No suitable constructor found for view {viewType.ToString()}");

            IView view = null;

            ThreadHelper.RunOnUIThread(() =>
            {
                view = constructor.Invoke(parameters) as IView;
            });

            if (view == null)
                throw new InvalidOperationException(
                    $"View {viewType.ToString()} does not implement the interface {nameof(IView)}");

            // Assign Binding Context
            if (_viewsByType.ContainsKey(viewType))
            {
                view.BindingContext = GetBindingContext(viewType);

                //// Pass parameter to view model if applicable
                //var model = view.BindingContext as IViewModel;
                //if (model != null)
                //    await model.OnNavigated(parameter);

                var multiView = view as IMultiView;

                if (multiView != null)
                    foreach (var p in multiView.Views)
                        p.BindingContext = GetBindingContext(p.GetType());
            }
            else
                throw new InvalidOperationException(
                    "No suitable view model found for view " + viewType.ToString());

            return Task.FromResult(view);
        }

        public void Map(Type viewType, Type viewModelType)
        {
            lock (_viewsByType)
                if (_viewsByType.ContainsKey(viewType))
                    _viewsByType[viewType] = new TypeDefinition() { Type = viewModelType };
                else
                    _viewsByType.Add(viewType, new TypeDefinition() { Type = viewModelType });
        }
    }
}
