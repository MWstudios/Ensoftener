# Ensoftener - A SharpDX.Direct2D1 extension for WinForms
Do you find SharpDX's implementation of Direct2D too confusing or limiting? Do you have trouble getting all those device contexts and shaders to work? SharpDX has been always like this, and with their forums being shut down and the amount of documentation getting close to zero, it can be even harder.
Something needs to change. We need to... ensoften it...

And that's when Ensoftener comes to play. It serves as an automation that simplifies the process of creating devices, shaders and SVG graphics down to a point where you don't need to do nearly anything. SharpDX may be dead, but here, it lives on.
## Features
* Multiple render targets and device contexts
* 32-bit float color depth for all renders
* Copying screen to a bitmap or applying a shader to the whole screen
* Better control over SVG images and their elements
* New `DeviceContext` extension methods that allow for chaining
* A set up pixel shader template ready for rendering
* An additional `Input` class containg all keyboard + mouse inputs

See the wiki for tutorials on how to install and use this library.