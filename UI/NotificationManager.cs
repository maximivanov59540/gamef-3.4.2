using UnityEngine;
using TMPro;

public class NotificationManager : MonoBehaviour
{
    [SerializeField] private GameObject notificationPanel;  // Панель с уведомлением
    [SerializeField] private TextMeshProUGUI notificationText;  // Текст уведомления
    [SerializeField] private float displayDuration = 3f;  // Время отображения уведомления

    private float timer;  // Таймер для отслеживания времени до скрытия
    private bool isNotificationActive = false;  // Флаг, показывающий, активно ли уведомление

    void Start()
    {
        HideNotification();  // Скрыть панель при старте
    }

    void Update()
    {
        // Если уведомление активно, уменьшаем таймер
        if (isNotificationActive)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                HideNotification();  // Скрыть панель, если время вышло
            }
        }
    }

    // Функция для показа уведомления
    public void ShowNotification(string message)
    {
        // --- ФИКС БАГА #13 ("Залипание") ---

        // "Беспрекословно" "обновляем" текст
        notificationText.text = message;

        // "Включаем" панель (даже если она уже "включена")
        notificationPanel.SetActive(true);
        isNotificationActive = true;

        // "Сбрасываем" таймер
        timer = displayDuration;
    }

    // Функция для скрытия уведомления
    private void HideNotification()
    {
        notificationPanel.SetActive(false);  // Скрываем панель
        isNotificationActive = false;  // Сбрасываем флаг активности
    }
}
