using System;
using System.Collections.Generic;
using System.IO;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ToolbarButton : Button
	{
		protected enum State
		{
			Default,
			Highlight,
			Press
		}

		private State state;
		private ITexture texture;
		private bool isChecked;
		private bool highlightable;
		private string text;

		public bool Selected { get; set; }

		public bool Checked
		{
			get { return isChecked; }
			set
			{
				if (isChecked != value) {
					isChecked = value;
					Window.Current.Invalidate();
				}
			}
		}

		public bool Highlightable
		{
			get { return highlightable; }
			set
			{
				if (highlightable != value) {
					highlightable = value;
					Window.Current.Invalidate();
				}
			}
		}

		public override ITexture Texture
		{
			get { return texture; }
			set
			{
				if (texture != value) {
					texture = value;
					Window.Current.Invalidate();
				}
			}
		}

		private ThemedSimpleText caption;
		public override string Text
		{
			get => text;
			set {
				if (value != text) {
					text = value;
					if (caption != null) {
						caption.Text = text;
						Window.Current.Invalidate();
					}
				}
			}
		}

		public string Tip { get; set; }

		public ToolbarButton()
		{
			highlightable = true;
			Nodes.Clear();
			Padding = new Thickness(2);
			Size = MinMaxSize = Theme.Metrics.DefaultToolbarButtonSize;
			DefaultAnimation.AnimationEngine = new AnimationEngineDelegate {
				OnRunAnimation = (animation, markerId, animationTimeCorrection) => {
					if (markerId == "Focus") {
						state = State.Highlight;
					} else if (markerId == "Press") {
						state = State.Press;
					} else {
						state = State.Default;
					}
					Window.Current.Invalidate();
					return true;
				}
			};
			CompoundPresenter.Add(new DelegatePresenter<Widget>(w => {
				w.PrepareRendererState();
				Renderer.Shader = Enabled ? ShaderId.Diffuse : ShaderId.Silhuette;
				Color4 bgColor, borderColor;
				GetColors(state, out bgColor, out borderColor);
				if (bgColor != Color4.Transparent) {
					Renderer.DrawRect(Vector2.Zero, Size, bgColor);
				}
				if (Texture != null) {
					var iconColor = Enabled ? GlobalColor : GlobalColor * ColorTheme.Current.Toolbar.ButtonDisabledColor;
					Renderer.DrawSprite(Texture, iconColor, ContentPosition, ContentSize, Vector2.Zero, Vector2.One);
				} else if (caption != null) {
					caption.Color = Enabled ? Theme.Colors.BlackText : Theme.Colors.GrayText;
				}
				if (borderColor != Color4.Transparent) {
					Renderer.DrawRectOutline(Vector2.Zero, Size, borderColor);
				}
			}));
			Awoke += Awake;
		}

		private static void Awake(Node owner)
		{
			var tb = (ToolbarButton)owner;
			tb.Tasks.Add(tb.ShowTipWhenMouseHangsOverButtonTask());
			tb.AddChangeWatcher(() => tb.Enabled, _ => Window.Current.Invalidate());
		}

		private IEnumerator<object> ShowTipWhenMouseHangsOverButtonTask()
		{
			while (true) {
				yield return null;
				if (IsMouseOver() && Tip != null) {
					var showTip = true;
					for (float t = 0; t < 0.5f; t += Task.Current.Delta) {
						if (!IsMouseOver()) {
							showTip = false;
							break;
						}
						yield return null;
					}
					if (showTip) {
						WidgetContext.Current.Root.Tasks.Add(ShowTipTask());
					}
				}
			}
		}

		private IEnumerator<object> ShowTipTask()
		{
			var window = WidgetContext.Current.Root;
			var tip = new Widget {
				Position = CalcPositionInSpaceOf(window) + new Vector2(Width * 0.66f, Height),
				Size = Vector2.Zero,
				LayoutCell = new LayoutCell { Ignore = true },
				Layout = new StackLayout(),
				Nodes = {
					new ThemedSimpleText { Text = Tip, Padding = new Thickness(4) },
					new ThemedFrame()
				}
			};
			tip.Updated += _ => tip.Size = tip.EffectiveMinSize;
			window.PushNode(tip);
			try {
				while (IsMouseOver()) {
					yield return null;
				}
			} finally {
				tip.Unlink();
			}
		}

		protected virtual void GetColors(State state, out Color4 bgColor, out Color4 borderColor)
		{
			if (Highlightable && state == State.Highlight) {
				bgColor = ColorTheme.Current.Toolbar.ButtonHighlightBackground;
				borderColor = ColorTheme.Current.Toolbar.ButtonHighlightBorder;
			} else if (Highlightable && state == State.Press) {
				bgColor = ColorTheme.Current.Toolbar.ButtonPressBackground;
				borderColor = ColorTheme.Current.Toolbar.ButtonPressBorder;
			} else if (Checked) {
				bgColor = ColorTheme.Current.Toolbar.ButtonCheckedBackground;
				borderColor = ColorTheme.Current.Toolbar.ButtonCheckedBorder;
			} else {
				bgColor = Color4.Transparent;
				borderColor = Selected
					? ColorTheme.Current.Toolbar.ButtonSelected
					: Color4.Transparent;
			}
		}

		public ToolbarButton(ITexture texture) : this()
		{
			Texture = texture;
		}

		public ToolbarButton(string text) : this()
		{
			caption = new ThemedSimpleText(text) {
				HAlignment = HAlignment.Center,
				VAlignment = VAlignment.Center
			};
			Text = text;
			MinMaxSize = new Vector2(caption.MeasureUncutText().X + 10, MaxSize.Y);
			AddNode(caption);
			caption.ExpandToContainerWithAnchors();
		}
	}
}
