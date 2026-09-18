using System;
using HLAS.Domain;

namespace HLAS.Application
{
    public sealed record AuthenticatedIdentity
    {
        public UserId UserId { get; }

        private AuthenticatedIdentity(UserId userId)
        {
            UserId = userId;
        }

        internal static AuthenticatedIdentity Create(UserId userId)
        {
            if (userId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Authenticated identity requires a non-empty UserId.",
                    nameof(userId));
            }

            return new AuthenticatedIdentity(userId);
        }
    }
}