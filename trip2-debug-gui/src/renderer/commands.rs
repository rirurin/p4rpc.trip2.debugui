use crate::Result;
use riri_imgui_vulkano::commands::{DrawBasic3d, DrawImgui, EndRenderPass, GpuCommandAllocator, GpuCommandBuilder, GpuCommandSet, GpuCommandUsageOnce, NextSubpass, StartRenderPass};
use riri_imgui_vulkano::descriptors::{Basic3dMVPUniform, ImguiOrthoUniform, LibDescriptorSets};
use riri_imgui_vulkano::geometry::{BasicDrawGeometry, ImguiGeometry};
use riri_imgui_vulkano::resources::{HasGraphicsPipeline, HasLogicalDevice, HasQueue, HasStandardMemoryAllocator};
use riri_imgui_vulkano::vertex::AppDrawData3D;
use vulkano::render_pass::Framebuffer;
use std::sync::Arc;
use riri_imgui_vulkano::pipeline::ImguiGraphicsPipeline;
use vulkano::command_buffer::PrimaryAutoCommandBuffer;
use vulkano::format::ClearValue;
use vulkano::pipeline::graphics::viewport::Viewport;

#[derive(Debug)]
pub struct AppGpuCommands {
    allocator: GpuCommandAllocator
}

impl AppGpuCommands {
    pub fn new<T>(context: &T) -> Self where T: HasLogicalDevice {
        Self {
            allocator: GpuCommandAllocator::new(context)
        }
    }

    pub fn allocator(&self) -> &GpuCommandAllocator {
        &self.allocator
    }
}

impl AppGpuCommands {
    pub fn create_gpu_commands<T>(
        &self,
        context: &T,
        viewport: &Viewport,
        framebuffer: Arc<Framebuffer>,
        pipeline: &ImguiGraphicsPipeline<0>,
        geom_imgui: ImguiGeometry,
        clear_color: ClearValue,
        descriptors: &mut LibDescriptorSets,
        ortho_uniform: &mut ImguiOrthoUniform,
    ) -> Result<Arc<PrimaryAutoCommandBuffer>>
    where T: HasLogicalDevice + HasStandardMemoryAllocator + HasQueue {
        ortho_uniform.create_descriptor_set(
            context, pipeline, descriptors, geom_imgui.get_orthographic_projection())?;
        let mut builder: GpuCommandBuilder<_, GpuCommandUsageOnce>
            = GpuCommandBuilder::new(&self.allocator, context)?;
        let clear_values = vec![Some(clear_color)];
        StartRenderPass::new(framebuffer.clone(), clear_values).build(&mut builder)?;
        DrawImgui::new(
            pipeline.graphics_pipeline(),
            &geom_imgui,
            viewport.clone(),
            descriptors,
            ortho_uniform.get()
        )?.build(&mut builder)?;
        EndRenderPass::new().build(&mut builder)?;
        Ok(builder.build()?)
    }
}