using System;
using System.Collections.Generic;

namespace ScreenCaptureApp.Services
{
    /// <summary>
    /// Простой DI-контейнер для управления сервисами приложения
    /// </summary>
    public class ServiceContainer
    {
        private static readonly Lazy<ServiceContainer> _instance = new Lazy<ServiceContainer>(() => new ServiceContainer());
        public static ServiceContainer Instance => _instance.Value;

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();

        private ServiceContainer() { }

        /// <summary>
        /// Регистрирует сервис как синглтон
        /// </summary>
        public void RegisterSingleton<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        /// <summary>
        /// Регистрирует фабрику для создания сервиса
        /// </summary>
        public void Register<T>(Func<T> factory) where T : class
        {
            _factories[typeof(T)] = () => factory();
        }

        /// <summary>
        /// Получает сервис из контейнера
        /// </summary>
        public T GetService<T>() where T : class
        {
            var type = typeof(T);

            // Проверяем, есть ли уже созданный экземпляр
            if (_services.TryGetValue(type, out var instance))
            {
                return (T)instance;
            }

            // Проверяем, есть ли фабрика для создания
            if (_factories.TryGetValue(type, out var factory))
            {
                var newInstance = factory();
                _services[type] = newInstance; // Кэшируем как синглтон
                return (T)newInstance;
            }

            throw new InvalidOperationException($"Service of type {type.Name} is not registered");
        }

        /// <summary>
        /// Проверяет, зарегистрирован ли сервис
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            var type = typeof(T);
            return _services.ContainsKey(type) || _factories.ContainsKey(type);
        }

        /// <summary>
        /// Очищает все зарегистрированные сервисы
        /// </summary>
        public void Clear()
        {
            _services.Clear();
            _factories.Clear();
        }
    }
} 