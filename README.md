# Ensoftener - A SharpDX.Direct2D extension for WinForms
Do you find SharpDX's implementation of Direct2D too confusing or limiting? Do you have trouble gwtting all those device contexts and shaders to work? SharpDX has been always like this, and with their forums being shut down and the amount of documentation getting close to zero, it can be even harder.
Something needs to change. We need to.. ensoften it...

And that's when Ensoftener comes to play. It simplifies the process of creating devices, shaders and SVG graphics down to a point where you don't need to do nearly anything.
## Installing
If you want to add Ensoftener to your project, you need .NET 5.0 and the following packages:
* SharpDX
* SharpDX.Desktop (ignore any errors)
* SharpDX.Direct2D1
* SharpDX.Mathematics
Then, download the library from the release tab and add it as a project reference in your solution.
Now it should be up and working!
## Getting it to work
In Form1.cs (or your form file), create a new `Form` class that you'll be running from Program.cs. The class will have a constructor, a `Run()` method and a method for rendering.
```csharp
public partial class Form1 : Form
{
	public Form1()
	{
	}
	public void Run()
	{
	}
	private void Render()
	{
	}
}
```
In Program.cs, change the `Main()` method to this:
```csharp
[MTAThread] //multithreaded application
static void Main()
{
	Application.SetHighDpiMode(HighDpiMode.SystemAware);
	Application.EnableVisualStyles();
	Application.SetCompatibleTextRenderingDefault(false); // default WinForms methods
	
	var game = new Form1(); game.Run(); // run the form class
}
```
In Form1's constructor, paste the following:
```csharp
public Form1()
{
	Global.Form = new("Window Title") { ClientSize = new(1600, 900) };
	Global.Initialize(1); // 1 device context for now
	InitializeComponent(); // default WinForms constructor
}
```
In the `Run()` method, reference `Global.Form` and your render method:
```csharp
using SharpDX.Windows;

public void Run()
{
	RenderLoop.Run(Global.Form, Render);
}
```
Now let's setup the `Render()` method. The `Global` class stores a list of device contexts, however so far we've created only one.
```csharp
private void Render()
{
    Global.BeginRender();
	Global.DCs[0].BeginDraw();
	Global.DCs[0].Clear(new RawColor4(1, 0, 0, 1));
	Global.DCs[0].EndDraw();
    Global.EndRender(); 
}
```
If you run the project, you should be seeing a red window.
## Creating a custom effect
SharpDX's `CustomEffectBase` class is by itself useless, however I've set up a much better class, `ShadedEffectBase`, which allows you to create a new shader within several lines. This tutorial assumes you already have a CSO shader file prepared, because covering the shading side and HLSL creation would be a long story. To summarize, you would download HLSL tools for Visual Studio and create a C++ project where you would write the HLSL files.
This is a basic HLSL file that shows a red and green color based on coordinates.
```hlsl
Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

float4 main( float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv0 : TEXCOORD0 ) : SV_Target
{
	return float4(uv0.xy, 0, 1); //R and G, B, A
}
```
First, you create a class that represents the shader. Copy-paste this class everytime you want to add a new one.
```csharp
public class Shader1 : ShadedEffectBase
{
	static Guid sGuid = Guid.NewGuid();
	
	public Shader1() : base(sGuid) { }
	
	public static void Register(SharpDX.Direct2D1.Factory2 d2f, string csoShaderFile)
	{
		SetShaderFile(csoShaderFile); d2f.RegisterEffect<Shader1>(); // set this to your classname
	}
}
```
In your Form, create an `Effect` object and add this to the constructor:
```csharp
private Effect testShader;

public Form1()
{
	Shader1.Register(Global.D2DFactory, "your shader.cso");
	testShader = new Effect<Shader1>(Global.DCs[0]);
}
```
Let's test the shader out. You can wither apply it to the whole screen...
```csharp
Global.DCs[0].RenderScreenShader(testShader);
```
...or you can load a bitmap and then apply the shader on that. In that case the shader only covers the bitmap, the rest of the screen remains untouched.
Feel free to render the bitmap itself without the shader, just to assure it shows up.
```csharp
var bmp = Global.DCs[0].LoadBitmapFromFile("your bitmap.png");
testShader.SetInput(0, bmp, true);
Global.DCs[0].DrawImage(testShader, new RawVector2(100, 100)); //bitmap position
bmp.Dispose();
```
## Translating, rotating and resizing bitmaps
If you've tried bitmap rendering in the previous tutorial, you've probably noticed that the bitmap had been rendered at a specified coordinate. But what if we wanted to scale or rotate it? For that case I've prepared another class, `EffectTransformer`, which is essentialy simplified `AffineTransform2D`.
Create a new `EffectTransformer` object in the constructor and turn the `bmp` from earlier into a global variable (because disposing and loading on every frame would ruin the transformer).
```csharp
private EffectTransformer tt1;
private Bitmap bmp;

public Form1()
{
	//... previous code from earlier
	
	bmp = Global.DCs[0].LoadBitmapFromFile("your bitmap.png");
	tt1 = new EffectTransformer(Global.DCs[0], testShader);
	tt1.X = 100; //position
	tt1.Y = 100;
	tt1.ScaleX = 2; //width multiplier
	tt1.Angle = 90; //rotation in radians
}
```
Now, we'll get rid of the previous code where we loaded a bitmap on the fly and rendered a shader on it. Instead, we'll render the newly created transformer.
```csharp
private void Render()
{
    Global.BeginRender();
	Global.DCs[0].BeginDraw();
	
	Global.DCs[0].Clear(new RawColor4(255, 0, 0, 255));
	Global.DCs[0].DrawEffectTransform(tt1);
	
	Global.DCs[0].EndDraw();
    Global.EndRender(); 
}
```
## One shader with multiple inputs
What used to be a painful implementation in SharpDX (speaking from my experience), is now merely a one-liner. Just add multiple `CustomEffectInput` attributes on top of your shader class. By default, each class has one input attribute, so if you want a shader to have, say, 4 inputs, add three more attributes:
```csharp
[CustomEffectInput("Source2"), CustomEffectInput("Source3"), CustomEffectInput("Source4")]
public class Shader1 : ShadedEffectBase
{
	//...
}
```
Then, specify three more inputs in the HLSL file. This shader blends 4 textures together, each in one corner:
```hlsl
Texture2D InputTexture1 : register(t0); //here
Texture2D InputTexture2 : register(t1);
Texture2D InputTexture3 : register(t2);
Texture2D InputTexture4 : register(t3);

SamplerState InputSampler1 : register(s0); //here
SamplerState InputSampler2 : register(s1);
SamplerState InputSampler3 : register(s2);
SamplerState InputSampler4 : register(s3);

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION,
	float4 uv0 : TEXCOORD0, float4 uv1 : TEXCOORD1, float4 uv2 : TEXCOORD2, float4 uv3 : TEXCOORD3) : SV_Target //and here
{
	//We're using uv0 only so all 4 textures will be lined up. Otherwise, 4 textures with different sizes would be offset.
	return lerp(lerp(InputTexture1.Sample(InputSampler1, uv0), InputTexture2.Sample(InputSampler2, uv0), uv0.x),
				lerp(InputTexture3.Sample(InputSampler3, uv0), InputTexture4.Sample(InputSampler4, uv0), uv0.x),
				uv0.y);
}
```
## Adding a constant buffer to your shader
A shader can have a constant buffer, that is, a struct with parameters that are passed to the GPU each frame. Since we can't access the shader class directly, we can only set parameters via `Effect.SetValue()` (blame Microsoft for this decision, not the SharpDX devs).
### Important notice
The values are passed to the GPU in chunks of 4 bytes. If you're passing a short or a boolean, you're gonna need to set the struct's layout to LayoutKind.Explicit and then manually adjust the values' offsets to multiples of 4, or they will all turn into garbage!

First, we add a struct and several properties to our shader class that will serve as the constant buffer:
```csharp
public struct Shader1Parameters
{
	public float param1, param2;
}

public class Shader1 : ShadedEffectBase
{
	public Shader1Parameters cBuffer;
	
	[PropertyBinding(0, "0", "0", "0")] //This attribute is required for Direct2D to access the constant buffer.
	public float Param1 { get { return cBuffer.param1; } set { cBuffer.param1 = value; } }
	
	[PropertyBinding(1, "0", "0", "0")] //The first number is the property's index, the rest isn't important. Remember to assign it a unique index!
	public float Param2 { get { return cBuffer.param2; } set { cBuffer.param2 = value; } }
	
    public override void UpdateConstants() //Passes the struct to GPU. Without this, it would be useless.
	{
		dInfo?.SetPixelConstantBuffer(ref cBuffer);
	}
	
	//... the code from earlier
}
```
Now we add the constant buffer in the HLSL file. Now you can use these variables inside your shader (read only, not for writing).
```hlsl
cbuffer values : register(b0)
{
    float param1;
    float param2;
}

//... the code from earlier
```
As stated before, the values can be set via `Effect.SetValue()`. Here, we set the index to 1 so we're accessing Param2.
```csharp
testShader.SetValue(1, 2.5f);
```
## Using 32-bit float color depth
Direct2D has the option to use float colors. However, of all components, only DeviceContext can work with them, which is pretty disappointing. You may not use float-based bitmaps, but there is a way to pass a float-based bitmap between multiple shaders and then render the result.
You'd be surprised that Direct2D actually doesn't differentiate from Blender's shading - Direct2D also has a node network of shaders, just not that visible. Each effect is a nodegraph by itself, so theoretically, if you were to nest multiple effects inside another effects, you could float pass floats between them with no problem.
And that's exactly how we're going to do this. Assume the following example, where `Shader1` contains `Shader2` and `Shader3`:
```csharp
public class Shader1 : ShadedEffectBase
{
	public Shader2 s1;
	public Shader3 s2;
	
	static Guid sGuid = Guid.NewGuid();
	
	public Shader1() : base(sGuid) { }
	
	public static void Register(SharpDX.Direct2D1.Factory2 d2f, string csoShaderFile) { SetShaderFile(csoShaderFile); d2f.RegisterEffect<Shader1>(); }
}
public class Shader2 : ShadedEffectBase
{
	static Guid sGuid = Guid.NewGuid();
	
	public Shader2() : base(sGuid) { }
	
	public static void Register(SharpDX.Direct2D1.Factory2 d2f, string csoShaderFile) { SetShaderFile(csoShaderFile); d2f.RegisterEffect<Shader2>(); }
}
public class Shader3 : ShadedEffectBase
{
	static Guid sGuid = Guid.NewGuid();
	
	public Shader3() : base(sGuid) { }
	
	public static void Register(SharpDX.Direct2D1.Factory2 d2f, string csoShaderFile) { SetShaderFile(csoShaderFile); d2f.RegisterEffect<Shader3>(); }
}
```
In `Shader1`, we'll have to manipulate the nodegraph so `Shader2` and `Shader3` become linked together. We'll leave `Shader1` out of the graph, as it's just a container for the other two.
SetGraph is a method each shader has but usually isn't necessary to modify. Now we'll be modifying it to suit our needs:
```csharp
public override void SetGraph(TransformGraph transformGraph)
{
	base.SetGraph(transformGraph); s1.SetGraph(transformGraph); s2.SetGraph(transformGraph); //Call SetGraph for the nested effects first, because they are never going to be called automatically.
	transformGraph.Clear(); transformGraph.AddNode(s1); transformGraph.AddNode(s2);          //Add both effects to Shader1's network.
	transformGraph.ConnectToEffectInput(0, s1, 0);                                           //"EffectInputs" are Shader1's input bitmaps from when we normally call Effect.SetInput().
	transformGraph.ConnectNode(s1, s2, 0); transformGraph.SetOutputNode(s2);                 //Link s1's output to s2's first and only input, then set s2 as the final output of Shader1's network.
}
```
Even though you're now using only one effect, you'll still have to register all three in the constructor. And even if Shader1 serves just as a container, you'll still have to give it a valid shader file. It can be any shader though, as it'll be never used.
```csharp
public Form1()
{
	Shader1.Register(Global.D2DFactory, "your shader.cso"); // dummy shader
	Shader2.Register(Global.D2DFactory, "your shader.cso");
	Shader3.Register(Global.D2DFactory, "your shader 2.cso");
	testShader = new Effect<Shader1>(Global.DCs[0]);
}
```
## Using SVG images
SVG in Direct2D is pretty much unexplored territory. Nobody knows it exists, documentation on Microsoft is vague and partially missing, and very few questions exist on StackOverflow. Even less explored is the SharpDX implementation, however of the little information I found, I've put together a `SvgImage` class.
The creation and use of SVGs is straight-forward: Load a SVG, change its position, size and rotation, then draw it. If you wish to modify more than just this, you have access to SVG's individual elements. However, in Direct2D you can't create elements or attributes, just delete them, so you'll have to call `Rebuild()` in that case to load the SVG again.
```csharp
SvgImage svg = new SvgImage(Global.DCs[0], "your image.svg");
svg.X = 200; svg.Y = 200;
Global.DCs[0].DrawSvgDocument(svg);
```
## Multiple render targets
So far, we've only referenced Global.DCs[0], which means we could only draw to one bitmap at a time. However, you can specify more than just one setup for drawing. In the constructor, set Global.Initialize() to 2 or more. You may now draw on multiple device contexts, but you can still display only one. Which one, is decided by Global.OutputDevice.
```csharp
public Form1()
{
	Global.Form = new("Window Title") { ClientSize = new(1600, 900) };
	Global.Initialize(2);
	Global.OutputDevice = 1;
	Global.DCs[1].ResizeWithScreen(true);
}
private void Render()
{
    Global.BeginRender();
	
	Global.DCs[0].BeginDraw();
	Global.DCs[0].Clear(new RawColor4(1, 0, 0, 1));
	Global.DCs[0].EndDraw();
	
	Global.DCs[1].BeginDraw();
	Global.DCs[1].Clear(new RawColor4(0, 1, 0, 1));
	Global.DCs[1].EndDraw();
	
    Global.EndRender(); 
}
```
Now, the screen should show up as green instead of red. But if you load a bitmap from the first context and draw it onto the second, it'll turn red again. It doesn't matter what context you use while creating an effect, a bitmap or an SVG. Any resource can be used in any context.

## Chaining DeviceContext methods together
Aside from the extensions used for the new features, there are also extension methods that copy the already existing ones, with a small difference: They make the DeviceContext return itself, which means you can chain methods behind each other.
As a result, all draw calls will no longer take an unnecassarily large amount of workspace. For example, this:
```csharp
Global.DCs[0].BeginDraw();
Global.DCs[0].Clear(new RawColor4(1, 0, 0, 1));
Global.DCs[0].FillRectangle(new RawRectangleF(0, 0, 100, 100), brush1);
Global.DCs[0].DrawImage(shader1);
Global.DCs[0].FillRectangle(new RawRectangleF(0, 100, 200, 400), brush2);
Global.DCs[0].DrawImage(shader2, new RawVector2(100, 100));
Global.DCs[0].DrawBitmap(someBitmap);
Global.DCs[0].EndDraw();
```
can be shortened into this. Notice that the new() commands don't need to have a class name specified, which is a feature of .NET 5.
```csharp
Global.DCs[0].ChainBeginDraw().ChainClear(new(1, 0, 0, 1)).ChainFillRectangle(new(0, 0, 100, 100), brush1).ChainDrawImage(shader1)
	.ChainFillRectangle(new(0, 100, 200, 400), brush2).ChainDrawImage(shader2, new(100, 100)).ChainDrawBitmap(someBitmap).EndDraw();
```