using System;
using System.Windows;
using System.Windows.Threading;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Threading.Tasks;

namespace ScreenCaptureApp.Helpers
{
    /// <summary>
    /// Helper class for initializing services
    /// </summary>
    public static class ServiceInitializer
    {
        /// <summary>
        /// Registers all application services in the DI container
        /// </summary>
        public static void RegisterAllServices()
        {
            try
            {
                var container = ServiceContainer.Instance;
                
                // Register basic services first (no dependencies)
                container.RegisterSingleton(new HotkeysService());
                container.RegisterSingleton(new WindowManagementService());
                container.RegisterSingleton(new ScreenshotService());
                container.RegisterSingleton(new DatabaseService());
                container.RegisterSingleton(new Mt4SocketService());
                container.RegisterSingleton(new BinaryOptionsSocketService());
                container.RegisterSingleton(new ToastNotifyService());
                container.RegisterSingleton(new JForexWindowsManagerService());
                
                // Register global state
                container.RegisterSingleton(new BrokerState());
                container.RegisterSingleton(new DurationState());
                container.RegisterSingleton(new TradeCounter());
                
                // Register services that depend on other services
                container.RegisterSingleton(new CaptureService());
                container.RegisterSingleton(new CaptureTrackingService());
                // Register PendingOrderAnalyzerService for P button functionality
                container.RegisterSingleton(new PendingOrderAnalyzerService());

                // Register HTTP Server Service
                container.RegisterSingleton(new HttpServerService());

                // Register ToolbarSettingsManager (singleton, in-memory)
                container.RegisterSingleton(new ToolbarSettingsManager());

                // Асинхронный запуск подключения BinaryOptionsSocketService
                var binaryOptionsSocketService = container.GetService<BinaryOptionsSocketService>();
                if (binaryOptionsSocketService != null)
                {
                    Task.Run(async () => await binaryOptionsSocketService.ConnectAsync());
                }

                // Асинхронный запуск HTTP Server Service
                var httpServerService = container.GetService<HttpServerService>();
                if (httpServerService != null)
                {
                    Task.Run(async () => await httpServerService.StartAsync());
                }
                
                Logger.LogInfo("All services registered in DI container");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to register services", ex);
                throw;
            }
        }

        /// <summary>
        /// Cleans up all services
        /// </summary>
        public static void CleanupServices()
        {
            try
            {
                var container = ServiceContainer.Instance;
                
                // Dispose services that implement IDisposable
                if (container.IsRegistered<HotkeysService>())
                {
                    var hotkeysService = container.GetService<HotkeysService>();
                    hotkeysService?.Dispose();
                }
                
                if (container.IsRegistered<Mt4SocketService>())
                {
                    var mt4Service = container.GetService<Mt4SocketService>();
                    mt4Service?.DisconnectAsync();
                }
                
                if (container.IsRegistered<BinaryOptionsSocketService>())
                {
                    var binaryOptionsService = container.GetService<BinaryOptionsSocketService>();
                    binaryOptionsService?.DisconnectAsync();
                }
                
                if (container.IsRegistered<HttpServerService>())
                {
                    var httpServerService = container.GetService<HttpServerService>();
                    httpServerService?.StopAsync();
                }
                
                Logger.LogInfo("All services cleaned up");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to cleanup services", ex);
            }
        }

        /// <summary>
        /// Initializes the hotkeys service with event handlers
        /// </summary>
        /// <param name="dispatcher">The dispatcher for UI operations</param>
        /// <param name="onSpaceKeyPressed">Action to execute when space key is pressed</param>
        /// <param name="onTKeyPressed">Action to execute when T key is pressed</param>
        /// <param name="onEscapeKeyPressed">Action to execute when escape key is pressed</param>
        /// <param name="onBackQuoteKeyPressed">Action to execute when backquote (`) key is pressed</param>
        public static void InitializeHotkeysService(
            Dispatcher dispatcher,
            Action onSpaceKeyPressed,
            Action onEscapeKeyPressed,
            Action onBackQuoteKeyPressed,
            Action onCKeyPressed = null,
            Action onWKeyPressed = null,
            Action onAKeyPressed = null,
            Action onDKeyPressed = null)
        {
            try
            {
                var hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
                
                hotkeysService.OnSpaceKeyPressed += () =>
                {
                    dispatcher.Invoke(onSpaceKeyPressed);
                };
                

                
                hotkeysService.OnEscapeKeyPressed += () =>
                {
                    dispatcher.Invoke(onEscapeKeyPressed);
                };
                
                hotkeysService.OnBackQuoteKeyPressed += () =>
                {
                    dispatcher.Invoke(onBackQuoteKeyPressed);
                };
                
                if (onCKeyPressed != null)
                {
                    hotkeysService.OnCKeyPressed += () =>
                    {
                        dispatcher.Invoke(onCKeyPressed);
                    };
                }
                
                if (onWKeyPressed != null)
                {
                    hotkeysService.OnWKeyPressed += () =>
                    {
                        dispatcher.Invoke(onWKeyPressed);
                    };
                }
                
                if (onAKeyPressed != null)
                {
                    hotkeysService.OnAKeyPressed += () =>
                    {
                        dispatcher.Invoke(onAKeyPressed);
                    };
                }
                

                
                if (onDKeyPressed != null)
                {
                    hotkeysService.OnDKeyPressed += () =>
                    {
                        dispatcher.Invoke(onDKeyPressed);
                    };
                }
                

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize hotkeys service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes the window management service
        /// </summary>
        /// <param name="dispatcher">The dispatcher for UI operations</param>
        public static void InitializeWindowManagementService(Dispatcher dispatcher)
        {
            try
            {
                var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
                windowManagementService.StatusUpdated += (statusMessage) =>
                {
                    dispatcher.Invoke(() =>
                    {
                        Logger.LogDebug($"Status: {statusMessage}");
                    });
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize window management service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes the MT4 socket service
        /// </summary>
        /// <param name="dispatcher">The dispatcher for UI operations</param>
        public static void InitializeMt4SocketService(Dispatcher dispatcher)
        {
            try
            {
                var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
                
                // Subscribe to events
                mt4SocketService.MessageReceived += (sender, message) =>
                {
                    dispatcher.Invoke(() => Mt4Helper.HandleMt4MessageReceived(message));
                };
                
                mt4SocketService.ConnectionStatusChanged += (sender, status) =>
                {
                    dispatcher.Invoke(() => Mt4Helper.HandleMt4ConnectionStatusChanged(status));
                };
                
                Logger.LogInfo("MT4 Socket Service initialized in MainWindow");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize MT4 Socket Service", ex);
                MessageBox.Show($"Failed to initialize MT4 Socket Service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes the Binary Options socket service
        /// </summary>
        /// <param name="dispatcher">The dispatcher for UI operations</param>
        public static void InitializeBinaryOptionsSocketService(Dispatcher dispatcher)
        {
            try
            {
                var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                // Subscribe to events
                binaryOptionsSocketService.MessageReceived += (sender, message) =>
                {
                    dispatcher.Invoke(() => PocketOptionHelper.HandlePocketOptionMessageReceived(message));
                };
                binaryOptionsSocketService.ConnectionStatusChanged += (sender, status) =>
                {
                    dispatcher.Invoke(() => PocketOptionHelper.HandlePocketOptionConnectionStatusChanged(status));
                };
                Logger.LogInfo("Binary Options Socket Service initialized in MainWindow");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize Binary Options Socket Service", ex);
                MessageBox.Show($"Failed to initialize Binary Options Socket Service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes the database service
        /// </summary>
        /// <param name="databaseService">The database service instance</param>
        /// <param name="windowManagementService">The window management service instance</param>
        public static void InitializeDatabaseService(DatabaseService databaseService, WindowManagementService windowManagementService)
        {
            try
            {
                // Инициализируем символы в базе данных, если их еще нет
                databaseService.InitializeSymbolsFromWindowManagementService(windowManagementService);
                
                Logger.LogInfo("Database Service initialized in MainWindow");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize Database Service", ex);
                MessageBox.Show($"Failed to initialize Database Service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initializes the HTTP Server service
        /// </summary>
        /// <param name="dispatcher">The dispatcher for UI operations</param>
        public static void InitializeHttpServerService(Dispatcher dispatcher)
        {
            try
            {
                var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
                
                // Subscribe to events
                httpServerService.PriceLevelReceived += (sender, priceLevelData) =>
                {
                    dispatcher.Invoke(() =>
                    {
                        Logger.LogInfo($"Price level received from GForex: {priceLevelData}");
                        // Здесь можно добавить дополнительную логику обработки ценовых уровней
                    });
                };
                
                httpServerService.StatusChanged += (sender, status) =>
                {
                    dispatcher.Invoke(() =>
                    {
                        Logger.LogInfo($"HTTP Server status: {status}");
                    });
                };
                
                Logger.LogInfo("HTTP Server Service initialized in MainWindow");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize HTTP Server Service", ex);
                MessageBox.Show($"Failed to initialize HTTP Server Service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 