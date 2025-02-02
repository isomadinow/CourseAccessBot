using CourseAccessBot.Models;
using System.Text.Json;

namespace CourseAccessBot.Repositories
{
    public class PaymentRepository
    {
        private readonly string _paymentsFilePath;
        private List<PaymentInfo> _payments;

        public PaymentRepository(string paymentsFilePath)
        {
            _paymentsFilePath = paymentsFilePath;
            _payments = new List<PaymentInfo>();
            LoadPayments();
        }

        private void LoadPayments()
        {
            if (File.Exists(_paymentsFilePath))
            {
                var json = File.ReadAllText(_paymentsFilePath);
                var data = JsonSerializer.Deserialize<List<PaymentInfo>>(json);
                if (data != null)
                    _payments = data;
            }
            else
            {
                _payments = new List<PaymentInfo>();
                SavePayments();
            }
        }

        private void SavePayments()
        {
            var json = JsonSerializer.Serialize(_payments, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_paymentsFilePath, json);
        }

        public PaymentInfo? GetPaymentById(Guid id) => _payments.FirstOrDefault(p => p.Id == id);

        public IEnumerable<PaymentInfo> GetAllPayments() => _payments;

        public void AddPayment(PaymentInfo payment)
        {
            _payments.Add(payment);
            SavePayments();
        }

        public void UpdatePayment(PaymentInfo payment)
        {
            var index = _payments.FindIndex(p => p.Id == payment.Id);
            if (index != -1)
            {
                _payments[index] = payment;
                SavePayments();
            }
        }

        public IEnumerable<PaymentInfo> GetPendingPayments() =>
            _payments.Where(p => p.Status == PaymentStatus.Pending);
    }
}
