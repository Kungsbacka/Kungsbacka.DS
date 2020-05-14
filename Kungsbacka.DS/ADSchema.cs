using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;

namespace Kungsbacka.DS
{
    public static class ADSchema
    {
        private class SchemaClass
        {
            public string LdapDisplayName { get; set; }
            public Guid SchemaGuid { get; set; }
            public string DistinguishedName { get; set; }
        }

        private class SchemaProperty
        {
            public string LdapDisplayName { get; set; }
            public Guid SchemaGuid { get; set; }
            public bool IsConfidential { get; set; }
        }

        private static readonly Dictionary<string, SchemaClass> schemaClassCache = new Dictionary<string, SchemaClass>();
        private static readonly Dictionary<string, SchemaProperty> schemaPropertyCache = new Dictionary<string, SchemaProperty>();

        private static SchemaClass GetSchemaClass(string className)
        {
            if (!schemaClassCache.TryGetValue(className, out SchemaClass schemaClass))
            {
                using (ActiveDirectorySchema schema = ActiveDirectorySchema.GetCurrentSchema())
                {
                    ActiveDirectorySchemaClass result = schema.FindClass(className);
                    System.DirectoryServices.DirectoryEntry directoryEntry = result.GetDirectoryEntry();
                    schemaClass = new SchemaClass()
                    {
                        LdapDisplayName = result.Name,
                        SchemaGuid = result.SchemaGuid,
                        DistinguishedName = (string)directoryEntry.Properties["distinguishedName"][0]
                    };
                    schemaClassCache.Add(result.Name, schemaClass);
                }
            }
            return schemaClass;
        }

        private static SchemaProperty GetSchemaProperty(string propertyName)
        {
            if (!schemaPropertyCache.TryGetValue(propertyName, out SchemaProperty schemaProperty))
            {
                using (ActiveDirectorySchema schema = ActiveDirectorySchema.GetCurrentSchema())
                {
                    ActiveDirectorySchemaProperty result = schema.FindProperty(propertyName);
                    System.DirectoryServices.DirectoryEntry directoryEntry = result.GetDirectoryEntry();
                    schemaProperty = new SchemaProperty()
                    {
                        LdapDisplayName = result.Name,
                        SchemaGuid = result.SchemaGuid,
                        IsConfidential = (((int)directoryEntry.Properties["searchFlags"][0] & 128) == 128)
                    };
                    schemaPropertyCache.Add(result.Name, schemaProperty);
                }
            }
            return schemaProperty;
        }

        public static string GetSchemaClassDistinguishedName(string className)
        {
            SchemaClass schemaClass = GetSchemaClass(className);
            return schemaClass.DistinguishedName;
        }

        public static bool IsAttributeConfidential(string attribute)
        {
            SchemaProperty schemaProperty = GetSchemaProperty(attribute);
            return schemaProperty.IsConfidential;
        }

        public static Guid GetClassSchemaGuid(string className)
        {
            SchemaClass schemaClass = GetSchemaClass(className);
            return schemaClass.SchemaGuid;
        }

        public static Guid GetAttributeSchemaGuid(string attribute)
        {
            SchemaProperty schemaProperty = GetSchemaProperty(attribute);
            return schemaProperty.SchemaGuid;
        }

    }
}
