using System;

namespace LabEquipmentCheckout
{
    public class LoanSlip
    {
        public const string Borrowing = "Đang mượn";
        public const string Returned = "Đã trả";
        public const string Overdue = "Quá hạn";

        public string Id { get; set; }
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string Equipment { get; set; }
        public int Quantity { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        // Tình trạng được TÍNH TỰ ĐỘNG, không lưu cứng
        public string Status => ComputeStatus(ReturnDate, DueDate);

        // Dùng cho cột "Ngày trả thực tế" trên lưới (trống nếu chưa trả)
        public string ReturnDateText => ReturnDate.HasValue ? ReturnDate.Value.ToString("dd/MM/yyyy") : "";

        public static string ComputeStatus(DateTime? returnDate, DateTime dueDate)
        {
            if (returnDate.HasValue) return Returned;
            return dueDate.Date < DateTime.Today ? Overdue : Borrowing;
        }
    }
}
