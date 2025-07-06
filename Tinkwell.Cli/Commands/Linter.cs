using System.Reflection;
using System.Text;

namespace Tinkwell.Cli.Commands;

abstract class Linter
{
    public interface IResult
    {
        IEnumerable<Linter.Rule> Rules { get; }

        IEnumerable<Linter.Issue> Issues { get; }

        IEnumerable<string> Messages { get; }

        bool Ignorable { get; }
    }

    public enum IssueSeverity
    {
        Minor,
        Warning,
        Error,
        Critical,
    }

    public record Issue(string Id, IssueSeverity Severity, string TargetType, string TargetName, string Message);

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StrictAttribute : Attribute { }

    public abstract class Rule
    {
        public virtual string Id
            => _id ??= ComputeCrc16(GetType().Name).ToString("00000");

        public virtual string Name
            => GetType().Name;

        public bool IsStrict
            => GetType().GetCustomAttribute<StrictAttribute>() is not null;

        protected static Issue? Ok() => null;

        protected Issue Minor<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Minor, typeof(TTarget).Name, targetName, message);

        protected Issue Warning<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Warning, typeof(TTarget).Name, targetName, message);

        protected Issue Error<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Error, typeof(TTarget).Name, targetName, message);

        protected Issue Critical<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Critical, typeof(TTarget).Name, targetName, message);

        private string? _id;

        private static ushort ComputeCrc16(string input)
        {
            const ushort poly = 0xA001;
            ushort crc = 0xFFFF;

            foreach (byte b in Encoding.UTF8.GetBytes(input))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    bool lsb = (crc & 1) == 1;
                    crc >>= 1;
                    if (lsb)
                        crc ^= poly;
                }
            }
            return crc;
        }
    }

    public string[] Exclusions { get; set; } = [];

    protected sealed class Result : IResult
    {
        public List<Rule> Rules { get; set; } = new();

        public List<Issue> Issues { get; set; } = new();

        public List<string> Messages { get; set; } = new();

        public bool Ignorable { get; set; }

        IEnumerable<Rule> IResult.Rules => Rules;
     
        IEnumerable<Issue> IResult.Issues => Issues;
        
        IEnumerable<string> IResult.Messages => Messages;
    }
}

abstract class Linter<TData> : Linter
{    
    public async Task<IResult> CheckAsync(string path, bool strict)
    {
        _strict = strict;
        LoadRules();

        try
        {
            var result =  Lint(await LoadFileAsync(path));
            result.Rules = result.Rules
                .OrderBy(x => x.Name)
                .ToList();
            result.Issues = result.Issues
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.Id)
                .ToList();
            result.Ignorable = !strict && result.Issues.All(x => x.Severity == IssueSeverity.Minor);
            return result;
        }
        catch (Exception e)
        {
            var result = new Result();
            result.Issues.Add(new Issue("00000", IssueSeverity.Critical, "File", path, e.Message));
            return result;
        }
    }

    protected abstract void LoadRules();

    protected abstract Task<TData> LoadFileAsync(string path);

    protected abstract Result Lint(TData data);

    protected IEnumerable<T> FindRules<T>()
    {
        return Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(T).IsAssignableFrom(type) && typeof(Rule).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .Where(type => _strict || type.GetCustomAttribute<StrictAttribute>() is null)
            .Select(type => (T)Activator.CreateInstance(type)!)
            .Where(rule => !Exclusions.Contains(((Rule)(object)rule!).Id, StringComparer.Ordinal));
    }

    private bool _strict;
}

