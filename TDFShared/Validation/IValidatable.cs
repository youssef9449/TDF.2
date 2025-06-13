using TDFShared.Validation.Results;

namespace TDFShared.Validation
{
    /// <summary>
    /// Interface for objects that can be validated
    /// </summary>
    public interface IValidatable
    {
        /// <summary>
        /// Validates the object and returns a validation result
        /// </summary>
        /// <returns>A validation result containing any validation errors</returns>
        ServiceValidationResult Validate();
    }
} 