using System;
using System.Runtime.InteropServices;
using vke;
using vkvg;
using Vulkan;

namespace Ensoftener.Vulkan2D;
[AttributeUsage(AttributeTargets.Class)] internal class VulkanAttribute : Attribute { }
/// <summary>The class containing everything necessary to run a Vulkan 2D window.</summary>
[Vulkan] public static class GVK
{
    public static VulkanForm Form { get; set; }
    /// <summary>Updates mouse+keyboard input in the main render thread. This is very important to look after, as updating input while another thread
    /// is running can have consequences if the other thread relies on this input.</summary>
    public static bool UpdateInputOnRender { get; set; }
    /// <summary>The main render loop of the Vulkan window. The device is necessary for creating more surfaces and the surface is the window's canvas.</summary>
    public static Action<vkvg.Device, Surface> RenderMethod { get; set; }
    public static void Initialize()
    {
        SwapChain.PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;
        Image.DefaultTextureFormat = VkFormat.R32g32b32a32Sfloat;
    }
    public static void Run(Action<vkvg.Device, vkvg.Surface> renderMethod) { RenderMethod = renderMethod; Form.Run(); }
    public class VulkanForm : VkWindow
    {
        /// <summary>The window title.</summary>
        public string Text { get; set; }
        vkvg.Device d; Surface s;
        FrameBuffers frameBuffers;
        GraphicPipeline plMain;
        DescriptorPool dsPool;
        DescriptorSetLayout dslMain;
        DescriptorSet dsUIimage, dsVKVGimg;
        Image vkvgImage;
        public VulkanForm(string title, uint width, uint height) : base(title, width, height, true) { }
        protected override void initVulkan()
        {
            base.initVulkan();
            d = new(instance.Handle, phy.Handle, dev.VkDev.Handle, presentQueue.qFamIndex, SampleCount.Sample_8);
            UpdateFrequency = 0;//update on each frame to have effective drawing perfs
            /*cmds = cmdPool.AllocateCommandBuffer(swapChain.ImageCount);

            dsPool = new DescriptorPool(dev, 2, new VkDescriptorPoolSize(VkDescriptorType.CombinedImageSampler, 2));
            dslMain = new DescriptorSetLayout(dev,
                new VkDescriptorSetLayoutBinding(0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler));

            GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault(VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1);
            cfg.RenderPass = new RenderPass(dev, swapChain.ColorFormat, VkSampleCountFlags.SampleCount1);
            cfg.Layout = new PipelineLayout(dev, dslMain);
            cfg.blendAttachments[0] = new VkPipelineColorBlendAttachmentState(true);
            cfg.AddShader(VkShaderStageFlags.Vertex, new VkShaderModule(), entryPoint: "#vke.FullScreenQuad.vert.spv");
            cfg.AddShader(VkShaderStageFlags.Fragment, new VkShaderModule(), entryPoint: "#polytest.simpletexture.frag.spv");
            plMain = new GraphicPipeline(cfg);
            dsUIimage = dsPool.Allocate(dslMain);
            dsVKVGimg = dsPool.Allocate(dslMain);*/
            //uiImageUpdate = new DescriptorSetWrites(dsUIimage, dslMain);
            GShared.OnQuit += (sender, e) => { d?.Dispose(); s?.Dispose(); Dispose(); Close(); };
        }
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr windowAfter, int x, int y, int width, int height, uint flags);
        protected override void render()
        {
            if (GShared.Quitting) { d?.Dispose(); s?.Dispose(); Dispose(); Close(); }
            Title = Text; //SetWindowPos(WindowHandle, IntPtr.Zero, 500, 500, 1600, 900, 0x1 | 0x200);
            base.render(); RenderMethod?.Invoke(d, s); if (UpdateInputOnRender) Input.Input.Update();
        }
        protected override void OnResize()
        {
            base.OnResize(); dev.WaitIdle();
            s?.Dispose(); s = new(d, (int)Width, (int)Height); s.Clear();
            VkImage srcImg = new((ulong)s.VkImage.ToInt64());
            for (int i = 0; i < swapChain.ImageCount; ++i)
            {
                cmds[i] = cmdPool.AllocateCommandBuffer(); cmds[i].Start();
                SetImageLayout(cmds[i].Handle, swapChain.images[i].Handle, VkImageAspectFlags.Color,
                    VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags.BottomOfPipe, VkPipelineStageFlags.Transfer);
                SetImageLayout(cmds[i].Handle, srcImg, VkImageAspectFlags.Color,
                    VkImageLayout.ColorAttachmentOptimal, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.Transfer);
                VkImageSubresourceLayers imgSubResLayer = new() { aspectMask = VkImageAspectFlags.Color, mipLevel = 0, baseArrayLayer = 0, layerCount = 1 };
                VkImageCopy cregion = new()
                {
                    srcSubresource = imgSubResLayer,
                    srcOffset = default,
                    dstSubresource = imgSubResLayer,
                    dstOffset = default,
                    extent = new VkExtent3D { width = (uint)s.Width, height = (uint)s.Height }
                };
                Vk.vkCmdCopyImage(cmds[i].Handle, srcImg, VkImageLayout.TransferSrcOptimal, swapChain.images[i].Handle, VkImageLayout.TransferDstOptimal, 1, ref cregion);
                SetImageLayout(cmds[i].Handle, swapChain.images[i].Handle, VkImageAspectFlags.Color,
                    VkImageLayout.TransferDstOptimal, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.BottomOfPipe);
                SetImageLayout(cmds[i].Handle, srcImg, VkImageAspectFlags.Color,
                    VkImageLayout.TransferSrcOptimal, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.ColorAttachmentOutput);
                cmds[i].End();
            }
            dev.WaitIdle();
        }
        static void SetImageLayout(VkCommandBuffer cmdbuffer, VkImage image, VkImageAspectFlags aspectMask, VkImageLayout oldImageLayout, VkImageLayout newImageLayout,
            VkPipelineStageFlags srcStageMask = VkPipelineStageFlags.AllCommands, VkPipelineStageFlags dstStageMask = VkPipelineStageFlags.AllCommands)
        {
            VkImageMemoryBarrier pImageMemoryBarriers = new()
            {
                sType = VkStructureType.ImageCreateInfo, srcQueueFamilyIndex = uint.MaxValue, dstQueueFamilyIndex = uint.MaxValue,
                oldLayout = oldImageLayout, newLayout = newImageLayout, image = image, subresourceRange = new(aspectMask)
            };
            switch (oldImageLayout)
            {
                case VkImageLayout.Undefined:
                    pImageMemoryBarriers.srcAccessMask = 0;
                    break;
                case VkImageLayout.Preinitialized:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.HostWrite;
                    break;
                case VkImageLayout.ColorAttachmentOptimal:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;
                case VkImageLayout.DepthStencilAttachmentOptimal:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.TransferSrcOptimal:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.TransferRead;
                    break;
                case VkImageLayout.TransferDstOptimal:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.TransferWrite;
                    break;
                case VkImageLayout.ShaderReadOnlyOptimal:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.ShaderRead;
                    break;
            }
            switch (newImageLayout)
            {
                case VkImageLayout.TransferDstOptimal:
                    pImageMemoryBarriers.dstAccessMask = VkAccessFlags.TransferWrite;
                    break;
                case VkImageLayout.TransferSrcOptimal:
                    pImageMemoryBarriers.srcAccessMask |= VkAccessFlags.TransferRead;
                    pImageMemoryBarriers.dstAccessMask = VkAccessFlags.TransferRead;
                    break;
                case VkImageLayout.ColorAttachmentOptimal:
                    pImageMemoryBarriers.srcAccessMask = VkAccessFlags.TransferRead;
                    pImageMemoryBarriers.dstAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;
                case VkImageLayout.DepthStencilAttachmentOptimal:
                    pImageMemoryBarriers.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.ShaderReadOnlyOptimal:
                    if (pImageMemoryBarriers.srcAccessMask == 0)
                    {
                        pImageMemoryBarriers.srcAccessMask = VkAccessFlags.TransferWrite | VkAccessFlags.HostWrite;
                    }

                    pImageMemoryBarriers.dstAccessMask = VkAccessFlags.ShaderRead;
                    break;
            }
            Vk.vkCmdPipelineBarrier(cmdbuffer, srcStageMask, dstStageMask, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, 1, ref pImageMemoryBarriers);
        }
    }
}