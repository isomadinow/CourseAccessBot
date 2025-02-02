namespace CourseAccessBot.Models
{
    public enum PaymentStatus
    {
        Pending,   // ожидает проверки
        Approved,  // одобрено
        Rejected   // отклонено
    }

    public class PaymentInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public long UserId { get; set; }          // ID пользователя Telegram
        public int CourseId { get; set; }         // Какой курс оплачивается
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public string? PhotoFileId { get; set; }  // FileId (скриншота/документа)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
