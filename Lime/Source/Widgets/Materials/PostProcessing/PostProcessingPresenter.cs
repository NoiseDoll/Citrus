using System;

namespace Lime
{
	public class PostProcessingPresenter : IPresenter
	{
		private readonly RenderChain renderChain = new RenderChain();
		private readonly IMaterial blendingDefaultMaterial = WidgetMaterial.GetInstance(Blending.Inherited, ShaderId.Inherited, 1);
		private readonly IMaterial blendingAddMaterial = WidgetMaterial.GetInstance(Blending.Add, ShaderId.Inherited, 1);
		private IMaterial material;
		private Blending blending;
		private ShaderId shader;
		private TextureBuffer sourceTextureBuffer;
		private TextureBuffer firstTemporaryBuffer;
		private TextureBuffer secondTemporaryBuffer;
		private HSLBuffer hslBuffer;
		private BlurBuffer blurBuffer;
		private BloomBuffer bloomBuffer;

		public RenderObject GetRenderObject(Node node)
		{
			var component = node.Components.Get<PostProcessingComponent>();
			if (component == null) {
				throw new InvalidOperationException();
			}

			const int MaxBufferSize = 2048;
			const float BufferReserve = 1.2f;
			var widget = (Widget)node;
			var asImage = widget as Image;
			var ro = RenderObjectPool<PostProcessingRenderObject>.Acquire();
			Size bufferSize;
			Size bufferSizeWithReserve;
			if (asImage != null) {
				bufferSize = bufferSizeWithReserve = asImage.Texture.ImageSize;
			} else {
				bufferSize = (Size)widget.Size;
				bufferSize = new Size(Math.Min(bufferSize.Width, MaxBufferSize), Math.Min(bufferSize.Height, MaxBufferSize));
				bufferSizeWithReserve = (Size)(widget.Size * BufferReserve);
				bufferSizeWithReserve = new Size(Math.Min(bufferSizeWithReserve.Width, MaxBufferSize), Math.Min(bufferSizeWithReserve.Height, MaxBufferSize));

				component.GetOwnerRenderObjects(renderChain, ro.Objects);
				renderChain.Clear();
			}

			// TODO: Buffers pool
			// TODO: Recreate buffers when image.Texture was changed
			if (asImage == null && (sourceTextureBuffer?.IsLessThen(bufferSize) ?? true)) {
				sourceTextureBuffer = new TextureBuffer(bufferSizeWithReserve);
			}
			if (component.HSLEnabled && (hslBuffer?.IsLessThen(bufferSize) ?? true)) {
				hslBuffer = new HSLBuffer(bufferSizeWithReserve);
			}
			if (component.BlurEnabled && (blurBuffer?.IsLessThen(bufferSize) ?? true)) {
				blurBuffer = new BlurBuffer(bufferSizeWithReserve);
			}
			if (component.BloomEnabled && (bloomBuffer?.IsLessThen(bufferSize) ?? true)) {
				bloomBuffer = new BloomBuffer(bufferSizeWithReserve);
			}
			if ((blurBuffer != null || bloomBuffer != null) && (firstTemporaryBuffer?.IsLessThen(bufferSize) ?? true)) {
				firstTemporaryBuffer = new TextureBuffer(bufferSizeWithReserve);
				secondTemporaryBuffer = new TextureBuffer(bufferSizeWithReserve);
			}

			ro.Texture = asImage?.Texture;
			ro.Material = asImage != null ? GetImageMaterial(asImage) : blendingDefaultMaterial;
			ro.LocalToWorldTransform = widget.LocalToWorldTransform;
			ro.Position = asImage?.ContentPosition ?? Vector2.Zero;
			ro.Size = asImage?.ContentSize ?? widget.Size;
			ro.Color = widget.GlobalColor;
			ro.UV0 = asImage?.UV0 ?? Vector2.Zero;
			ro.UV1 = asImage?.UV1 ?? Vector2.One;
			ro.DebugViewMode = component.DebugViewMode;
			ro.SourceTextureBuffer = sourceTextureBuffer;
			ro.FirstTemporaryBuffer = firstTemporaryBuffer;
			ro.SecondTemporaryBuffer = secondTemporaryBuffer;
			ro.HSLBuffer = hslBuffer;
			ro.HSLMaterial = component.HSLMaterial;
			ro.HSLEnabled = component.HSLEnabled;
			ro.HSL = component.HSL;
			ro.BlurBuffer = blurBuffer;
			ro.BlurMaterial = component.BlurMaterial;
			ro.BlurEnabled = component.BlurEnabled;
			ro.BlurRadius = component.BlurRadius;
			ro.BlurTextureScaling = component.BlurTextureScaling * 0.01f;
			ro.BlurAlphaCorrection = component.BlurAlphaCorrection;
			ro.BlurBackgroundColor = component.BlurBackgroundColor;
			ro.BloomBuffer = bloomBuffer;
			ro.BloomMaterial = component.BloomMaterial;
			ro.BloomEnabled = component.BloomEnabled;
			ro.BloomStrength = component.BloomStrength;
			ro.BloomBrightThreshold = component.BloomBrightThreshold * 0.01f;
			ro.BloomGammaCorrection = component.BloomGammaCorrection;
			ro.BloomTextureScaling = component.BloomTextureScaling * 0.01f;
			ro.BloomColor = component.BloomColor;
			ro.NoiseEnabled = component.NoiseEnabled && component.NoiseTexture != null && !component.NoiseTexture.IsStubTexture;
			ro.NoiseStrength = component.NoiseStrength * 0.01f;
			ro.NoiseTexture = component.NoiseTexture;
			ro.SoftLightMaterial = component.SoftLightMaterial;
			ro.OverallImpactEnabled = component.OverallImpactEnabled;
			ro.OverallImpactColor = component.OverallImpactColor;
			ro.BlendingDefaultMaterial = blendingDefaultMaterial;
			ro.BlendingAddMaterial = blendingAddMaterial;
			return ro;
		}

		private IMaterial GetImageMaterial(Image image)
		{
			if (image.CustomMaterial != null) {
				return image.CustomMaterial;
			}
			if (material != null && blending == image.GlobalBlending && shader == image.GlobalShader) {
				return material;
			}
			blending = image.GlobalBlending;
			shader = image.GlobalShader;
			return material = WidgetMaterial.GetInstance(blending, shader, 1);
		}

		// TODO: Fix HitTest of child nodes
		public bool PartialHitTest(Node node, ref HitTestArgs args) => DefaultPresenter.Instance.PartialHitTest(node, ref args);

		public IPresenter Clone() => new PostProcessingPresenter();

		public enum DebugViewMode
		{
			None,
			Original,
			Bloom
		}

		internal class TextureBuffer
		{
			private RenderTexture finalTexture;

			protected bool IsDirty { get; set; } = true;

			public Size Size { get; }
			public RenderTexture Texture => finalTexture ?? (finalTexture = new RenderTexture(Size.Width, Size.Height));

			public TextureBuffer(Size size)
			{
				Size = size;
			}

			public bool IsLessThen(Size size) => Size.Width < size.Width || Size.Height < size.Height;
			public void MarkAsDirty() => IsDirty = true;
		}

		internal class HSLBuffer : TextureBuffer
		{
			private Vector3 hsl = new Vector3(float.NaN, float.NaN, float.NaN);

			public HSLBuffer(Size size) : base(size) { }

			public bool EqualRenderParameters(Vector3 hsl) => !IsDirty && this.hsl == hsl;

			public void SetParameters(Vector3 hsl)
			{
				IsDirty = false;
				this.hsl = hsl;
			}
		}

		internal class BlurBuffer : TextureBuffer
		{
			private float radius = float.NaN;
			private float textureScaling = float.NaN;
			private float alphaCorrection = float.NaN;
			private Color4 backgroundColor = Color4.Zero;

			public BlurBuffer(Size size) : base(size) { }

			public bool EqualRenderParameters(float radius, float textureScaling, float alphaCorrection, Color4 backgroundColor) =>
				!IsDirty &&
				this.radius == radius &&
				this.textureScaling == textureScaling &&
				this.alphaCorrection == alphaCorrection &&
				this.backgroundColor == backgroundColor;

			public void SetParameters(float radius, float textureScaling, float alphaCorrection, Color4 backgroundColor)
			{
				IsDirty = false;
				this.radius = radius;
				this.textureScaling = textureScaling;
				this.alphaCorrection = alphaCorrection;
				this.backgroundColor = backgroundColor;
			}
		}

		internal class BloomBuffer : TextureBuffer
		{
			private float strength = float.NaN;
			private float brightThreshold = float.NaN;
			private Vector3 gammaCorrection = -Vector3.One;
			private float textureScaling = float.NaN;

			public BloomBuffer(Size size) : base(size) { }

			public bool EqualRenderParameters(float strength, float brightThreshold, Vector3 gammaCorrection, float textureScaling) =>
				!IsDirty &&
				this.strength == strength &&
				this.brightThreshold == brightThreshold &&
				this.gammaCorrection == gammaCorrection &&
				this.textureScaling == textureScaling;

			public void SetParameters(float strength, float brightThreshold, Vector3 gammaCorrection, float textureScaling)
			{
				IsDirty = false;
				this.strength = strength;
				this.brightThreshold = brightThreshold;
				this.gammaCorrection = gammaCorrection;
				this.textureScaling = textureScaling;
			}
		}
	}
}