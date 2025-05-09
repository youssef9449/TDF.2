using System;

namespace TDFAPI.Exceptions
{
    /// <summary>
    /// Exception thrown when an entity is not found
    /// </summary>
    public class EntityNotFoundException : DomainException
    {
        public string EntityName { get; }
        public string EntityId { get; }

        public EntityNotFoundException(string entityName, string entityId)
            : base($"Entity '{entityName}' with ID '{entityId}' was not found.", "entity_not_found")
        {
            EntityName = entityName;
            EntityId = entityId;
        }

        public EntityNotFoundException(string entityName, int entityId)
            : this(entityName, entityId.ToString())
        {
        }

        public EntityNotFoundException(string entityName, Guid entityId)
            : this(entityName, entityId.ToString())
        {
        }
    }
} 