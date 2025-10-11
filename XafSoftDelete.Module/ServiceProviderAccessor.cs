using System;
using System.Threading;

namespace XafSoftDelete.Module {
    // Simple ambient accessor for the current IServiceProvider during operations
    // such as the database updater where the ObjectSpace may not carry DI.
    public static class ServiceProviderAccessor {
        private static readonly AsyncLocal<IServiceProvider> current = new AsyncLocal<IServiceProvider>();
        public static IServiceProvider? Current {
            get => current.Value;
            set => current.Value = value;
        }
        // Global fallback provider for hosts that build their own IServiceProvider (Win builder path)
    public static IServiceProvider Global { get; set; }
    }
}
