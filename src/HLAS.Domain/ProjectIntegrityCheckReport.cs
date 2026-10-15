using System.Collections.Generic;

namespace HLAS.Domain
{
    public sealed record ProjectIntegrityCheckReport
    {
        public ProjectId ProjectId { get; }
        public IReadOnlyList<ProjectIntegrityCheckItem> Items { get; }

        public ProjectIntegrityCheckReport(
            ProjectId projectId,
            IReadOnlyList<ProjectIntegrityCheckItem> items)
        {
            if (projectId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ProjectId may not be empty.",
                    nameof(projectId));
            }

            ArgumentNullException.ThrowIfNull(items);

            if (items.Count == 0)
            {
                throw new ArgumentException(
                    "Integrity report must explicitly identify its check coverage.",
                    nameof(items));
            }

            ProjectIntegrityCheckItem[] snapshot =
                new ProjectIntegrityCheckItem[items.Count];

            for (int index = 0; index < items.Count; index++)
            {
                if (items[index] is null)
                {
                    throw new ArgumentException(
                        "Integrity check items may not be null.",
                        nameof(items));
                }

                snapshot[index] = items[index];
            }

            ProjectId = projectId;
            Items = Array.AsReadOnly(snapshot);
        }
    }
}