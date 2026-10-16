using System;

namespace RepairServiceTracker
{
    public class RepairOrder
    {
        public const string StatusNew = "Mới tiếp nhận";
        public const string StatusRepairing = "Đang sửa";
        public const string StatusWaitingParts = "Chờ linh kiện";
        public const string StatusDone = "Hoàn tất";
        public const string StatusCancelled = "Đã hủy";

        public string Id { get; set; }
        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public string DeviceType { get; set; }
        public string Description { get; set; }
        public DateTime ReceivedDate { get; set; }
        public decimal EstimatedCost { get; set; }
        public string Status { get; set; }
        public string ImagePath { get; set; }   // có thể null

        // Đơn "chưa hoàn tất" = chưa Hoàn tất và chưa Đã hủy
        public bool IsUnfinished => Status != StatusDone && Status != StatusCancelled;
    }
}
