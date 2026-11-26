namespace CafeHub.Application.Attendance
{
    public sealed record AttendanceScanDecision(Guid BranchId, Guid SessionId, Guid? CheckOutRecordId)
    {
        public bool IsCheckOut => CheckOutRecordId.HasValue;
    }
}
