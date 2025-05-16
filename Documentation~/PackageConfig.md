
Below is a comprehensive documentation of the **PackageConfig** class in the `Lingotion.Thespeon.API` namespace which lets you configure certain parameters within the Lingotion Thespeon Engine to optimize its fit into your specific Unity project. This documentation describes each field’s intent, usage, and any validation rules or optional behaviors. 

  

---

## Table of Contents

- [Table of Contents](#table-of-contents)
- [1. PackageConfig Class](#1-packageconfig-class)
  - [1.1. **Purpose and overview**](#11-purpose-and-overview)
  - [1.2. **Fields**](#12-fields)
  - [1.3. **Use**](#13-use)
- [2. Example](#2-example)

  

---

## 1. PackageConfig Class

In this section you will find a description of the `PackageConfig` class and its purpose in your Unity Project. 

### 1.1. **Purpose and overview**
In the Lingotion Thespeon Unity package, a lot of parameter decisions have already been made for you and are therefore hidden under the hood. The PackageConfig class is the structure which lets you override these decisions and supply your own parameters to tailor the Thespeon Engine to your liking. There are two ways of doing so: changing parameters globally and locally. Changing parameters globally allows you to set your own parameters such that they apply to all actions you make in your scene. You may also make local changes to the parameters to only apply for a single Thespeon Engine Synthesis call. Any field may and should be left out if you do not intend to use it.

Below is an overview of the fields and methods in the PackageConfig class.
```csharp
public class PackageConfig
{
	[JsonProperty("useAdaptiveFrameBreakScheduling", NullValueHandling = NullValueHandling.Ignore)]
	public bool? useAdaptiveFrameBreakScheduling { get; set; }
	[JsonProperty("targetFrameTime", NullValueHandling = NullValueHandling.Ignore)]
	public double? targetFrameTime { get; set; }
	[JsonProperty("overshootMargin", NullValueHandling = NullValueHandling.Ignore)]
	public float? overshootMargin { get; set; }


	/// <summary>
	/// Copy constructor for PackageConfig.
	/// </summary>
	/// <param name="config">The PackageConfig instance to copy from.</param>
	public PackageConfig(PackageConfig config)
	{
		useAdaptiveFrameBreakScheduling = config.useAdaptiveFrameBreakScheduling;                
		targetFrameTime = config.targetFrameTime;
		overshootMargin = config.overshootMargin;
	}

	/// <summary>
	/// Sets the configuration values from another PackageConfig instance by overwriting all non-null values in overrideConfig and returns the new instance. A validation of config values with eventual revision also takes place.
	/// </summary>
	/// <param name="overrideConfig">The PackageConfig instance to override values from.</param>
	/// <returns>A new PackageConfig instance with the overridden values.</returns>
	public PackageConfig SetConfig(PackageConfig overrideConfig)

	public override string ToString()

}
```

  


---

### 1.2. **Fields**
In the current iteration of the Lingotion Thespeon Engine the following fields are available for configuration with more fields to come in the future:

- **useAdaptiveFrameBreakScheduling** (`bool?`, default true): 
	The Thespeon Engine runs its synthesis in a coroutine to let you as a developer retain control of your game's frames. An adaptive algorithm supplements a heuristic in making the decision of when to yield for a frame and this boolean value allows you to turn the adaptive algorithm off.
- **targetFrameTime** (`double?`, default `0.005`):  
	The above mentioned heuristic utilizes a frame time budget which is decided by this parameter. The Thespeon Engine coroutine runs at the end of every frame and the time allocated (in seconds) will determine how long time of each frame this is allowed to take. Due to asynchronous schedules this is not exact and as such it is a soft limit and may as such be overdrawn. The adaptive algorithm attempts to find better solutions over time to better fit this requirement. 
- **overshootMargin** (`float?`, default `1`):  
	The overshoot margin is a multiplier, no less than 1, of how much above the targetFrameTime an operation is allowed to go before being flagged by the adaptive algorithm. A larger value will make the adaptive algorithm less aggressive.

Any of these fields are nullable which means a change in configuration, either locally or globally, does *not* necessitate supplying all values. Setting new ones will only consider supplied fields if they are not null and keep the old values for the remainder.


### 1.3. **Use**

The`PackageConfig` class is found under the `Lingotion.Thespeon.API` namespace. Just like with the `UserModelInput` class, the `PackageConfig` class supports the JSON format, being fully serializable. Constructing the class can therefore be done in two ways, either by use of the class constructor, or by deserialization from a json file on disk. 

In the namespace, you have access the `ThespeonAPI` static class which has the following methods: 
- `public static PackageConfig GetConfig(string synthID=null)` 
	Returns the global config if `synthID` is null, otherwise the local config used by the synthetization with ID `synthID`. 
* `public static void SetGlobalConfig(PackageConfig config)`
	Overrides properties in the global config with the provided non-null properties in the supplied config.
Furthermore, the MonoBehaviour script ThespeonEngine has a Synthesize method which takes an optional PackageConfig object which will specify the deviations from the global config to use during that particular synthetization. In the [following section](#2-example) you will find a code example.


---

## 2. Example

Here is a simple example of how to construct and use the PackageConfig class in your Unity project to tailor your usage of the Thespeon Engine. Instantiation of the object can be done either explicitly with the constructor or through the use of deserialization using the Newtonsoft package.


```csharp

using UnityEngine;
using Newtonsoft.Json;
using Lingotion.Thespeon.API;

  

public class ExampleUsage : MonoBehaviour
{
    void Start()
    {
        UserModelInput input = //from some source - see [Thespeon Advanced Control Guide](./how-to-control-thespeon.md) for details.
		PackageConfig myGlobalConfigs = new PackageConfig {
            useAdaptiveFrameBreakScheduling = true,
            targetFrameTime = 0.005, // 5 ms
            overshootMargin = 1.5f
        };
        //PackageConfig myGlobalConfigs = JsonConvert.DeserializeObject<UserModelInput>(someJsonString);
       ThespeonAPI.SetGlobalConfig(myGlobalConfigs);

		PackageConfig myLocalDeviations = new PackageConfig {
            targetFrameTime = 0.003, // 3 ms
        };

		GameObject.Find("My NPC Object").GetComponent<ThespeonEngine>().Synthesize(input); //Here global configs will apply.
		GameObject.Find("My NPC Object").GetComponent<ThespeonEngine>().Synthesize(input, config: myLocalDeviations); //Here global configs with specified local deviations will apply.
    }

}

```

