using System;

namespace HLAS.Domain
{
    public readonly record struct ProjectJmfReviewCaseId(Guid Value)
    {
        public static ProjectJmfReviewCaseId CreateNew()
        {
            return new ProjectJmfReviewCaseId(Guid.NewGuid());
        }
    }
}