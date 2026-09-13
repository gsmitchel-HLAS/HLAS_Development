namespace HLAS.Domain
{
    public readonly record struct ProjectRole
    {
        public string Value { get; }

        private ProjectRole(string value)
        {
            Value = value;
        }

        public static ProjectRole Technician { get; } = new("Technician");
        public static ProjectRole Senior { get; } = new("Senior");
        public static ProjectRole Admin { get; } = new("Admin");
    }
}