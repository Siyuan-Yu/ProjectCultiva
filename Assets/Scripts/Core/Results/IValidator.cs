namespace XianXia.Core.Results
{
    public interface IValidator<in T>
    {
        void Validate(T target, ValidationReport report);
    }
}
