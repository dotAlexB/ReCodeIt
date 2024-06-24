using Mono.Cecil;
using Mono.Cecil.Rocks;
using ReCodeIt.Models;
using ReCodeIt.Utils;
using System.Text.RegularExpressions;

namespace ReCodeIt.ReMapper;

internal static class RenameHelper
{
    private static List<string> TokensToMatch => DataProvider.Settings.AutoMapper.TokensToMatch;

    /// <summary>
    /// Only used by the manual remapper, should probably be removed
    /// </summary>
    /// <param name="score"></param>
    public static AssemblyDefinition RenameAll(ScoringModel score, AssemblyDefinition definition, bool direct = false)
    {
        var types = definition.MainModule.GetAllTypes();

        // Rename all fields and properties first
        if (DataProvider.Settings.Remapper.MappingSettings.RenameFields)
        {
            RenameAllFields(score.Definition.Name, score.ReMap.NewTypeName, types);
        }

        if (DataProvider.Settings.Remapper.MappingSettings.RenameProperties)
        {
            RenameAllProperties(score.Definition.Name, score.ReMap.NewTypeName, types);
        }

        if (!direct)
        {
            RenameType(types, score);
        }

        Logger.Log($"{score.Definition.Name} Renamed.", ConsoleColor.Green);

        return definition;
    }

    /// <summary>
    /// Only used by the manual remapper, should probably be removed
    /// </summary>
    /// <param name="score"></param>
    public static void RenameAllDirect(RemapModel remap, AssemblyDefinition definition, TypeDefinition type)
    {
        var directRename = new ScoringModel
        {
            Definition = type,
            ReMap = remap
        };
        RenameAll(directRename, definition, true);
    }

    /// <summary>
    /// Rename all fields recursively, returns number of fields changed
    /// </summary>
    /// <param name="oldTypeName"></param>
    /// <param name="newTypeName"></param>
    /// <param name="typesToCheck"></param>
    /// <returns></returns>
    public static int RenameAllFields(
        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDefinition> typesToCheck,
        int overAllCount = 0)
    {
        foreach (var type in typesToCheck)
        {
            var fields = type.Fields
                .Where(field => field.Name.IsFieldOrPropNameInList(TokensToMatch));

            if (!fields.Any()) { continue; }

            int fieldCount = 0;
            foreach (var field in type.Fields)
            {
                if (field.FieldType.Name.TrimAfterSpecialChar() == oldTypeName)
                {
                    var newFieldName = GetNewFieldName(newTypeName, field.IsPublic || field.IsFamily, fieldCount);

                    // Dont need to do extra work
                    if (field.Name == newFieldName) { continue; }

                    Logger.Log($"Renaming field on type {type.Name} with name `{field.Name}` to `{newFieldName}`", ConsoleColor.Green);

                    field.Name = newFieldName;

                    UpdateMethodFieldNames(typesToCheck, field, newFieldName);

                    fieldCount++;
                    overAllCount++;
                }
            }
        }

        return overAllCount;
    }

    private static void UpdateMethodFieldNames(IEnumerable<TypeDefinition> typesToCheck, FieldReference oldRef, string newName)
    {
        foreach (var type in typesToCheck)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                CheckMethodForRequiredChanges(method, oldRef, newName);
            }
        }
    }

    private static void CheckMethodForRequiredChanges(MethodDefinition method, FieldReference oldRef, string newName)
    {
        // Get the method body
        var body = method.Body;

        // Iterate through all instructions in the method body
        for (int i = 0; i < body.Instructions.Count; i++)
        {
            var instruction = body.Instructions[i];

            // Check if the instruction is a field reference
            if (instruction.Operand is FieldReference fieldRef && fieldRef.Resolve() is FieldDefinition fieldDef)
            {
                // Check if the field reference matches the old field name and type
                if (fieldRef.Name != oldRef.Name) { continue; }

                if (fieldDef.IsPublic)
                {
                    newName = Regex.Replace(newName, "^_", "", RegexOptions.Compiled);
                }
                else
                {
                    if (newName.ToCharArray()[0] != '_')
                    {
                        newName = newName.Insert(0, "_");
                    }
                }

                Logger.Log($"Updating method reference on type {fieldRef.DeclaringType} in method `{method.Name}` from `{fieldRef.Name}` to `{newName}`", ConsoleColor.Green);

                // Replace the old FieldReference with the new one
                fieldRef.Name = newName;
            }
        }
    }

    /// <summary>
    /// Rename all properties recursively, returns number of fields changed
    /// </summary>
    /// <param name="oldTypeName"></param>
    /// <param name="newTypeName"></param>
    /// <param name="typesToCheck"></param>
    /// <returns></returns>
    public static int RenameAllProperties(
        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDefinition> typesToCheck,
        int overAllCount = 0)
    {
        foreach (var type in typesToCheck)
        {
            var properties = type.Properties
                .Where(prop => prop.Name.IsFieldOrPropNameInList(TokensToMatch));

            if (!properties.Any()) { continue; }

            int propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.PropertyType.Name == oldTypeName)
                {
                    var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                    // Dont need to do extra work
                    if (property.Name == newPropertyName) { continue; }

                    Logger.Log($"Renaming original property type name: `{property.PropertyType.Name}` with name `{property.Name}` to `{newPropertyName}`", ConsoleColor.Green);
                    property.Name = newPropertyName;
                    propertyCount++;
                    overAllCount++;
                }
            }

            if (type.HasNestedTypes)
            {
                RenameAllProperties(oldTypeName, newTypeName, type.NestedTypes, overAllCount);
            }
        }

        return overAllCount;
    }

    public static string GetNewFieldName(string NewName, bool isPrivate, int fieldCount = 0)
    {
        var discard = isPrivate ? "_" : "";
        string newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        return $"{discard}{char.ToLower(NewName[0])}{NewName[1..]}{newFieldCount}";
    }

    public static string GetNewPropertyName(string newName, int propertyCount = 0)
    {
        return propertyCount > 0 ? $"{newName}_{propertyCount}" : newName;
    }

    private static void RenameType(IEnumerable<TypeDefinition> typesToCheck, ScoringModel score)
    {
        foreach (var type in typesToCheck)
        {
            if (type.HasNestedTypes)
            {
                RenameType(type.NestedTypes, score);
            }

            if (score.Definition.Name is null) { continue; }

            if (score.ReMap.SearchParams.IsNested is true &&
                type.IsNested && type.Name == score.Definition.Name)
            {
                type.Name = score.ProposedNewName;
            }

            if (type.FullName == score.Definition.Name)
            {
                type.Name = score.ProposedNewName;
            }
        }
    }
}