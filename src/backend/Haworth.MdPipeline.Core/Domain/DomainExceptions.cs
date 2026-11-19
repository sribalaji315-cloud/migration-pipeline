namespace Haworth.MdPipeline.Core.Domain;

public class DomainValidationException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);
