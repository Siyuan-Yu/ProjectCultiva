namespace XianXia.Core.Results
{
    /// <summary>
    /// Stable business error codes. Not localized; display mapping is out of M1 scope.
    /// </summary>
    public enum ErrorCode
    {
        None = 0,
        Unknown = 1,
        InvalidArgument = 2,
        NotFound = 3,
        AlreadyExists = 4,
        InvalidOperation = 5,
        ValidationFailed = 6,
        InvalidDefinitionId = 7,
        ContentLoadFailed = 8,
        DuplicateDefinitionId = 9,
        MissingRequiredField = 10,
        IncompatibleContentVersion = 11,
        OrderRejected = 12,
        ActionCannotStart = 13,
        ActionFailed = 14,
        SnapshotInvalid = 15,
        SnapshotVersionMismatch = 16,
        EntityNotFound = 17,
        ComponentMissing = 18
    }
}
