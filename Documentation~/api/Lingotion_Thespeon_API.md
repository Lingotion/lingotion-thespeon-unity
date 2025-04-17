# Lingotion.Thespeon.API API Documentation

## Class `PackageConfig`

> ### Constructor `PackageConfig()`
> 
> Initializes a new empty instance of the PackageConfig class.
> 
> 
> ### Constructor `PackageConfig(PackageConfig config)`
> 
> Copy constructor for PackageConfig.
> **Parameters:**
> 
> - `config`: The PackageConfig instance to copy from.
> 
> 
> 
> ### Method `PackageConfig SetConfig(PackageConfig overrideConfig)`
> 
> Sets the configuration values from another PackageConfig instance by overwriting all non-null values in overrideConfig and returns the new instance. A validation of config values with eventual revision also takes place.
> **Parameters:**
> 
> - `overrideConfig`: The PackageConfig instance to override values from.
> 
> **Returns:** A new PackageConfig instance with the overridden values.
> 
> 
> 
> ### Method `void ValidateAndRevise()`
> 
> Validates the configuration values and revises them in place if invalid.
> 