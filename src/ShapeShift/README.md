# ShapeShift

***One serializer for all formats***

## Features

* PolyType-based serialization means a shared set of attributes for your data models regardless of the use case.
* Serialize any object to any encoding simply by writing the encoder/decoder.

## Consumption note

This package is not directly installed by applications, typically.
Instead, an application should install one or more of the format-specific packages (search for "ShapeShift" on NuGet).
For example, to use ShapeShift with YAML, install the [ShapeShift.Yaml](https://www.nuget.org/packages/ShapeShift.Yaml/) package.

Developing format-specific packages of course requires a direct dependency on ShapeShift.
