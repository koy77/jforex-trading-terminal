using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Observers
{
    /// <summary>
    /// Интерфейс для наблюдателей событий нового бара
    /// </summary>
    public interface INewBarObserver
    {
        /// <summary>
        /// Обрабатывает событие нового бара
        /// </summary>
        /// <param name="newBarEvent">Данные о новом баре</param>
        void OnNewBarReceived(NewBarEvent newBarEvent);
        
        /// <summary>
        /// Уникальный идентификатор наблюдателя
        /// </summary>
        string ObserverId { get; }
    }
}
