namespace Bot
{
    [System.AttributeUsage(System.AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    sealed class GitHashAttribute : System.Attribute
    {
        public string Hash { get; }
        public GitHashAttribute(string hsh)
        {
            this.Hash = hsh;
        }
    }
}
