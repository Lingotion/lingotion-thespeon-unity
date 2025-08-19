// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using System.Linq;

namespace Lingotion.Thespeon.Inputs
{
    /// <summary>
    /// This class is used to define characters and their associated modules.
    /// </summary>
    [CreateAssetMenu(fileName = "NewThespeonCharacter", menuName = "Lingotion Thespeon/Character Asset")]
    public class ThespeonCharacterAsset : ScriptableObject
    {
        public string actorName;
        public ModuleType moduleType;
    }

    /// <summary>
    /// Collection of control characters used in Thespeon.
    /// </summary>
    public static class ControlCharacters
    {
        public const char Pause = '⏸';
    }

    /// <summary>
    /// Helper class to manage Thespeon characters and their modules.
    /// </summary>
    public static class ThespeonCharacterHelper
    {
        /// <summary>
        /// Retrieves all imported characters and their associated modules.
        /// </summary>
        /// <returns>A list of tuples for each pair of character name and module type.</returns>
        public static List<(string characterName, ModuleType moduleType)> GetAllCharactersAndModules()
        {
            return PackManifestHandler.Instance
                .GetAllActors()
                .SelectMany(characterName =>
                PackManifestHandler.Instance.GetAllModuleTypesForActor(characterName)
                    .Select(moduleType => (characterName, moduleType)))
                .ToList();
        }

        /// <summary>
        /// Retrieves all module types for a specific character.
        /// </summary>
        /// <param name="characterName">The name of the character to retrieve modules for.</param>
        /// <returns>A list of ModuleType values available for the specified character.</returns>
        public static List<ModuleType> GetAllModulesForCharacter(string characterName)
        {
            return PackManifestHandler.Instance.GetAllModuleTypesForActor(characterName);
        }
    }
}