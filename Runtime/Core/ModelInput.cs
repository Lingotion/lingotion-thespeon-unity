// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System.Collections.Generic;
using System.Linq;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Base class for model inputs, providing common properties and methods for all model inputs.
    /// </summary>
    /// <typeparam name="ModelInputType">The specific ModelInput type that inherits from this class.</typeparam>
    /// <typeparam name="InputSegmentType">The specific ModelInputSegment type that this input uses.</typeparam>
    public abstract class ModelInput<ModelInputType, InputSegmentType>
    where ModelInputType : ModelInput<ModelInputType, InputSegmentType>
    where InputSegmentType : ModelInputSegment
    {
        public string ActorName;
        public ModuleType ModuleType;
        public Emotion DefaultEmotion;
        public ModuleLanguage DefaultLanguage;
        public List<InputSegmentType> Segments = new();

        /// <summary>
        /// Deep copy constructor for ModelInput.
        /// </summary>
        /// <param name="other">The ModelInput instance to copy from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the provided ModelInput instance is null.</exception>
        public ModelInput(ModelInput<ModelInputType, InputSegmentType> other)
        {
            if (other == null)
            {
                throw new System.ArgumentNullException(nameof(other), "Cannot copy from a null ModelInput instance.");
            }
            ActorName = other.ActorName;
            ModuleType = other.ModuleType;
            DefaultEmotion = other.DefaultEmotion;
            DefaultLanguage = ModuleLanguage.CopyOrNull(other.DefaultLanguage);
            Segments = other.Segments.Select(segment => (InputSegmentType)segment.DeepCopy()).ToList();
        }
        /// <summary>
        /// Constructor for ModelInput.
        /// Initializes a new instance of ModelInput with the specified actor name, module type, default emotion, and default language.
        /// </summary>
        /// <param name="actorName">The name of the actor.</param>
        /// <param name="segments">A list of ModelInputSegment instances representing the segments of the input.</param>
        /// <param name="defaultLanguage">The default language to be used.</param>
        /// <param name="defaultEmotion">The default emotion to be used.</param>
        /// <param name="moduleType">The type of the module.</param>
        /// <exception cref="System.ArgumentException">Thrown if the actor name is null or empty, or if the segments list is null or empty.</exception>
        public ModelInput(List<InputSegmentType> segments, string actorName, ModuleType moduleType = ModuleType.None, Emotion defaultEmotion = Emotion.None, string defaultLanguage = null, string defaultDialect = null)
        {
            if (segments == null || segments.Count == 0)
            {
                throw new System.ArgumentException("Segments cannot be null or empty.", nameof(segments));
            }
            if (string.IsNullOrEmpty(actorName))
            {
                List<string> actors = PackManifestHandler.Instance.GetAllActors();
                if (actors == null || actors.Count == 0)
                {
                    throw new System.ArgumentException("No actors found. Make sure to import an actor pack.");
                }
                LingotionLogger.Warning("Actor name is null or empty. Defaulting to first found actor: " + actors[0]);
                actorName = actors[0];
            }
            if (moduleType == ModuleType.None)
            {
                List<ModuleType> modules = PackManifestHandler.Instance.GetAllModuleTypesForActor(actorName);
                modules = modules.OrderByDescending(m => m).ToList();
                if (modules.Count == 0)
                {
                    throw new System.ArgumentException($"No module types found for actor '{actorName}'. Please ensure you have the correct actor pack imported.", nameof(moduleType));
                }
                moduleType = modules[0];
                LingotionLogger.Warning($"ModuleType is set to None. Defaulting to '{moduleType}'.");
            }
            ActorName = actorName;
            Segments = segments;
            ModuleType = moduleType;
            DefaultEmotion = defaultEmotion;
            List<ModuleLanguage> availableLangs = PackManifestHandler.Instance.GetAllSupportedLanguages(actorName, moduleType);
            if (availableLangs == null || availableLangs.Count == 0)
            {
                throw new System.ArgumentException($"No languages found for actor '{actorName}' and module type '{moduleType}'. Please ensure you have the correct actor pack imported.");
            }
            if (string.IsNullOrEmpty(defaultLanguage) && !string.IsNullOrEmpty(defaultDialect))
            {
                defaultLanguage = ModuleLanguage.NoLang;
            }
            DefaultLanguage = ModuleLanguage.BestMatch(availableLangs, defaultLanguage, defaultDialect);
            if (DefaultLanguage == null)
            {
                LingotionLogger.Warning("You selected no default language. Defaulting to first available language: " + availableLangs[0]);
                DefaultLanguage = availableLangs[0];
            }
            foreach (InputSegmentType segment in Segments)
            {
                if (segment.Language != null && segment.Language.Iso639_2 != null)
                {

                    segment.Language = ModuleLanguage.BestMatch(availableLangs, segment.Language.Iso639_2, segment.Language.Iso3166_1);
                    if (segment.Language.Equals(DefaultLanguage))
                    {
                        segment.Language = null;
                    }
                }
            }
        }


        public ModelInput(List<InputSegmentType> segments, string actorName = null, Emotion defaultEmotion = Emotion.None, ModuleType moduleType = ModuleType.None, ModuleLanguage defaultLanguage = null)
        {
            if (segments == null || segments.Count == 0)
            {
                throw new System.ArgumentException("Segments cannot be null or empty.", nameof(segments));
            }
            if (string.IsNullOrEmpty(actorName))
            {
                List<string> actors = PackManifestHandler.Instance.GetAllActors();
                if (actors == null || actors.Count == 0)
                {
                    throw new System.ArgumentException("No actors found. Make sure to import an actor pack.");
                }
                LingotionLogger.Warning("Actor name is null or empty. Defaulting to first found actor: " + actors[0]);
                actorName = actors[0];
            }
            if (moduleType == ModuleType.None)
            {
                List<ModuleType> modules = PackManifestHandler.Instance.GetAllModuleTypesForActor(actorName);
                modules = modules.OrderByDescending(m => m).ToList();
                if (modules.Count == 0)
                {
                    throw new System.ArgumentException($"No module types found for actor '{actorName}'. Please ensure you have the correct actor pack imported.", nameof(moduleType));
                }
                moduleType = modules[0];
                LingotionLogger.Warning($"ModuleType is set to None. Defaulting to '{moduleType}'.");
            }
            ActorName = actorName;
            Segments = segments;
            ModuleType = moduleType;
            DefaultEmotion = defaultEmotion;
            DefaultLanguage = ModuleLanguage.CopyOrNull(defaultLanguage);
        }

        public abstract string ToJson();

    }


}