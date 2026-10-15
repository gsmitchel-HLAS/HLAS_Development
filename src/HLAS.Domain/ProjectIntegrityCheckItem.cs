using System;

namespace HLAS.Domain
{
    public sealed record ProjectIntegrityCheckItem
    {
        public string CheckCode { get; }
        public ProjectIntegrityCheckStatus Status { get; }
        public string Detail { get; }

        public ProjectIntegrityCheckItem(
            string checkCode,
            ProjectIntegrityCheckStatus status,
            string detail)
        {
            if (string.IsNullOrWhiteSpace(checkCode))
            {
                throw new ArgumentException(
                    "CheckCode may not be blank.",
                    nameof(checkCode));
            }

            if (status != ProjectIntegrityCheckStatus.Verified &&
                status != ProjectIntegrityCheckStatus.Pending &&
                status != ProjectIntegrityCheckStatus.IntegrityFailure &&
                status != ProjectIntegrityCheckStatus.NotChecked)
            {
                throw new ArgumentException(
                    "Invalid project integrity check status.",
                    nameof(status));
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                throw new ArgumentException(
                    "Detail may not be blank.",
                    nameof(detail));
            }

            CheckCode = checkCode;
            Status = status;
            Detail = detail;
        }
    }
}