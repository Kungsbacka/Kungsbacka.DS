using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices;

namespace Kungsbacka.DS
{
    public static class Schema
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

        private static Dictionary<string, SchemaClass> schemaClassCache = new Dictionary<string, SchemaClass>();
        private static Dictionary<string, SchemaProperty> schemaPropertyCache = new Dictionary<string, SchemaProperty>();

        private static SchemaClass GetSchemaClass(string className)
        {
            SchemaClass schemaClass;
            if (!schemaClassCache.TryGetValue(className, out schemaClass))
            {
                using (var schema = ActiveDirectorySchema.GetCurrentSchema())
                {
                    var result = schema.FindClass(className);
                    var directoryEntry = result.GetDirectoryEntry();
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
            SchemaProperty schemaProperty;
            if (!schemaPropertyCache.TryGetValue(propertyName, out schemaProperty))
            {
                using (var schema = ActiveDirectorySchema.GetCurrentSchema())
                {
                    var result = schema.FindProperty(propertyName);
                    var directoryEntry = result.GetDirectoryEntry();
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
