using System;
using System.Collections.Generic;

namespace CC.Game.Boot
{
    /// <summary>
    /// Minimal service locator for the few persistent services created in the Boot scene
    /// (GameFlow, InputRouter, later Audio and Leaderboard). Game rules must not use it.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Registry = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Registry[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (Registry.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException(
                $"Service {typeof(T).Name} is not registered. Start the game from the Boot scene.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Registry.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        public static void Clear() => Registry.Clear();
    }
}
