using System.Linq;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.SceneView
{
	public class WidgetsPivotMarkPresenter
	{
		public WidgetsPivotMarkPresenter(SceneView sceneView)
		{
			sceneView.Frame.CompoundPostPresenter.Add(new DelegatePresenter<Widget>(RenderWidgetsPivotMark));
		}

		private void RenderWidgetsPivotMark(Widget canvas)
		{
			if (
				Core.Document.Current.ExpositionMode ||
				Core.Document.Current.PreviewAnimation
			) {
				return;
			}
			var widgets = WidgetsWithDisplayedPivot().ToList();
			if (widgets.Count == 0) {
				return;
			}
			canvas.PrepareRendererState();
			var iconSize = new Vector2(16, 16);
			foreach (var widget in widgets) {
				var t = NodeIconPool.GetTexture(widget.GetType());
				var p = widget.CalcPositionInSpaceOf(canvas);
				var position = p - iconSize / 2;
				position.X = (float)System.Math.Truncate(position.X);
				position.Y = (float)System.Math.Truncate(position.Y);
				Renderer.DrawSprite(t, Color4.White, position, iconSize, Vector2.Zero, Vector2.One);
			}
		}

		public static IEnumerable<Widget> WidgetsWithDisplayedPivot()
		{
			if (
				!SceneUserPreferences.Instance.DisplayPivotsForAllWidgets &&
				!SceneUserPreferences.Instance.DisplayPivotsForInvisibleWidgets
			) {
				return Enumerable.Empty<Widget>();
			}

			var widgets = Core.Document.Current.Container.Nodes.Editable().OfType<Widget>();
			if (!SceneUserPreferences.Instance.DisplayPivotsForAllWidgets) {
				widgets = widgets.Where(w => w.Color.A == 0 || !w.Visible);
			}
			return widgets;
		}
	}
}
