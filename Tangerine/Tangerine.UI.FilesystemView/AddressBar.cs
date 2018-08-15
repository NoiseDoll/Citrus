using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.FilesystemView
{
	public class AddressBar : Toolbar
	{
		public enum AddressBarState
		{
			PathBar,
			Editor
		}
		private string buffer = "C:\\";
		private AddressBarState state;
		private PathBar pathBar;
		private ThemedEditBox editor;
		private Func<string, bool> openPath;

		public string Path
		{
			get
			{
				return buffer;
			}
			set
			{
				buffer = AdjustPath(value);
			}
		}

		public AddressBar(Func<string, bool> openPath)
		{
			this.openPath = openPath;
			Layout = new StackLayout();
			state = AddressBarState.PathBar;
			CreatePathBar();
			CreateEditor();
			Updating += (float delta) => {
				if (
					editor.IsFocused() &&
					state != AddressBarState.Editor
				) {
					state = AddressBarState.Editor;
					editor.Text = buffer;
					RemovePathBar();
				}
				if (
					state == AddressBarState.Editor &&
					!editor.IsFocused()
				) {
					FlipState();
				}
			};
		}

		private IEnumerator<object> ShowAlertTask(string message)
		{
			yield return Task.WaitWhile(() => Input.ConsumeKeyPress(Key.Enter));

			var dialog = new AlertDialog(message);
			dialog.Show();
		}

		private string AdjustPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) {
				return buffer;
			}
			if (path.Length < 3) {
				Tasks.Add(ShowAlertTask("The size of the path is less than the permissible."));
				return buffer;
			}

			if (path.Contains("..")) {
				var i = 0;
				
				if (path.EndsWith("/../")) {
					i = 4;
				} else if (path.EndsWith("\'..\'")) {
					i = 4;
				} else if (path.EndsWith("/..")) {
					i = 3;
				} else if (path.EndsWith("\'..")) {
					i = 4;
				} else if (path.EndsWith("..")) {
					i = 2;
				}

				if (i != 0) {
					if (new System.IO.DirectoryInfo(path.Remove(path.Length - i)).Parent == null) {
						path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
					} else {
						path = System.IO.Path.GetDirectoryName(path.Remove(path.Length - i));
					}
				}
			}

			char[] charsToTrim = { '.', ' ' };
			path = path.Trim(charsToTrim);

			path = path.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);

			//If the user added many slashes
			string doubleDirectorySeparator = string.Empty;
			doubleDirectorySeparator += System.IO.Path.DirectorySeparatorChar;
			doubleDirectorySeparator += System.IO.Path.DirectorySeparatorChar;
			if (path.Contains(doubleDirectorySeparator)) {
				Tasks.Add(ShowAlertTask("The path is in an invalid format."));
				return buffer;
			}

			if (
				path[path.Length - 1] == System.IO.Path.DirectorySeparatorChar &&
				path[path.Length - 2] != System.IO.Path.VolumeSeparatorChar
			) {
				path = path.Remove(path.Length - 1);
			}

			return path;
		}

		public static string PathToFolderPath(string path)
		{
			if (System.IO.Path.GetExtension(path) != string.Empty) {
				if (!System.IO.Directory.Exists(path)) {
					var i = path.Length - 1;
					var c = path[path.Length - 1];
					while (c != System.IO.Path.DirectorySeparatorChar) {
						path = path.Remove(i);
						i--;
						c = path[i];
					}
				}
			}
			if (
				path[path.Length - 1] == System.IO.Path.DirectorySeparatorChar &&
				path[path.Length - 2] != System.IO.Path.VolumeSeparatorChar
			) {
				path = path.Remove(path.Length - 1);
			}
			return path;
		}

		public void SetFocusOnEditor()
		{
			if (state != AddressBarState.Editor) {
				FlipState();
				editor.SetFocus();
			}
		}

		private void FlipState()
		{
			if (state == AddressBarState.Editor) {
				state = AddressBarState.PathBar;
				editor.Text = "";
				CreatePathBar();
			} else {
				state = AddressBarState.Editor;
				RemovePathBar();
				editor.Text = buffer;
			}
		}

		private void CreateEditor()
		{
			Nodes.Add(editor = new ThemedEditBox());
			editor.LayoutCell = new LayoutCell(Alignment.LeftCenter);
			editor.Updating += (float delta) => {
				if (editor.Input.WasKeyPressed(Key.Enter)) {
					var adjustedText = AdjustPath(editor.Text);
					if (openPath(adjustedText)) {
						buffer = PathToFolderPath(adjustedText);
						FlipState();
					} else {
						editor.Text = buffer;
					}
				}
			};
		}

		private void CreatePathBar()
		{
			Nodes.Push(pathBar = new PathBar(openPath, this));
			pathBar.LayoutCell = new LayoutCell(Alignment.LeftCenter);
			pathBar.Updating += UpdatingPathBar;
		}

		private void UpdatingPathBar(float delta)
		{
			if (pathBar.IsMouseOver() && pathBar.Input.WasMouseReleased(Key.Mouse0)) {
				FlipState();
			}
		}

		private void RemovePathBar()
		{
			Nodes.Remove(pathBar);
			pathBar.Updating -= UpdatingPathBar;
			pathBar = null;
		}
	}

	public class PathBar : Widget
	{
		private string buffer;
		private List<string> topFoldersPaths;
		private Func<string, bool> openPath;
		private PathBarButton[] buttons;
		private PathArrowButton rootArrowButton;

		public PathBar(Func<string, bool> openPath, AddressBar addressBar)
		{
			this.openPath = openPath;
			buffer = addressBar.Path;
			Layout = new HBoxLayout();
			LayoutCell = new LayoutCell(Alignment.LeftCenter);
			Padding = new Thickness(1);
			CreateButtons();

			Updating += (float delta) => {
				if (!buffer.Equals(addressBar.Path)) {
					buffer = addressBar.Path;
					UpdatePathBar();
				}
			};
		}

		private void CreateButtons()
		{
			topFoldersPaths = GetTopFoldersPaths(buffer);
			buttons = new PathBarButton[topFoldersPaths.Count];

			Nodes.Add(rootArrowButton = new PathArrowButton(openPath));
			for (var i = topFoldersPaths.Count - 1; i >= 0; i--) {
				Nodes.Add(buttons[i] = new PathBarButton(openPath, topFoldersPaths[i]));
			}
		}

		private void RemoveButtons()
		{
			for (var i = topFoldersPaths.Count - 1; i >= 0; i--) {
				Nodes.Remove(buttons[i]);
			}
			Nodes.Remove(rootArrowButton);
		}

		private void UpdatePathBar()
		{
			RemoveButtons();
			CreateButtons();
		}

		public static List<string> GetTopFoldersPaths(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) {
				return null;
			}
			var topFolders = new List<string>();
			topFolders.Add(path);
			var p = System.IO.Path.GetDirectoryName(topFolders[topFolders.Count - 1]);
			while (p != null) {
				topFolders.Add(p);
				p = System.IO.Path.GetDirectoryName(topFolders[topFolders.Count - 1]);
			}
			return topFolders;
		}
	}

	public enum PathBarButtonState
	{
		Normal,
		Hover,
		Press
	}

	public class PathBarButton : Widget
	{
		private PathBarButtonState state;
		private PathFolderButton folderButton;
		private PathArrowButton arrowButton;

		public PathBarButton(Func<string, bool> openPath, string path) : base()
		{
			Layout = new HBoxLayout();
			HitTestTarget = true;

			folderButton = new PathFolderButton(openPath, path);
			arrowButton = new PathArrowButton(openPath, path);

			Nodes.Add(folderButton);
			Nodes.Add(arrowButton);

			Updating += (float delta) => {
				if (arrowButton.ArrowState == PathArrowButtonState.Expanded) {
					state = PathBarButtonState.Press;
				} else {
					if (IsMouseOverThisOrDescendant()) {
						if (
							folderButton.WasClicked() ||
							arrowButton.WasClicked()
						) {
							state = PathBarButtonState.Press;
						} else {
							state = PathBarButtonState.Hover;
						}
					} else {
						state = PathBarButtonState.Normal;
					}
				}
				folderButton.SetState(state);
				arrowButton.SetState(state);
			};
		}
	}

	public class PathButtonPresenter : ThemedButton.ButtonPresenter
	{
		private ColorGradient innerGradient;
		private Color4 outline;

		public void SetState(PathBarButtonState state)
		{
			CommonWindow.Current.Invalidate();
			switch (state) {
				case PathBarButtonState.Normal:
					innerGradient = Theme.Colors.PathBarButtonNormal;
					outline = Theme.Colors.PathBarButtonOutlineNormal;
					break;
				case PathBarButtonState.Hover:
					innerGradient = Theme.Colors.PathBarButtonHover;
					outline = Theme.Colors.PathBarButtonOutlineHover;
					break;
				case PathBarButtonState.Press:
					innerGradient = Theme.Colors.PathBarButtonPress;
					outline = Theme.Colors.PathBarButtonOutlinePress;
					break;
			}
		}

		public override void Render(Node node)
		{
			var widget = node.AsWidget;
			widget.PrepareRendererState();
			Renderer.DrawVerticalGradientRect(Vector2.Zero, widget.Size, innerGradient);
			Renderer.DrawRectOutline(Vector2.Zero, widget.Size, outline);
		}
	}

	public class PathFolderButton : ThemedButton
	{
		private new PathButtonPresenter Presenter;
		public PathArrowButton arrowButton;
		public PathBarButtonState State;

		public PathFolderButton(Func<string, bool> openPath, string path) : base()
		{
			Text = GetName(path);
			Presenter = new PathButtonPresenter();
			base.Presenter = Presenter;
			MinMaxHeight = 20;
			MinMaxWidth = Renderer.MeasureTextLine(Text, Theme.Metrics.TextHeight, 0).X + 7;
			Gestures.Add(new ClickGesture(0, () => openPath(path)));
			Gestures.Add(new ClickGesture(1, () => SystemShellContextMenu.Instance.Show(path)));
		}

		public void SetState(PathBarButtonState state)
		{
			Presenter.SetState(state);
		}

		public static string GetName(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) {
				return null;
			} else if (
				path[path.Length - 1] == System.IO.Path.DirectorySeparatorChar &&
				path[path.Length - 2] == System.IO.Path.VolumeSeparatorChar
			) {
				// Root
				return path.Remove(path.Length - 1);
			} else {
				// Folder
				int i;
				for (i = path.Length - 1; i >= 0; i--) {
					if (path[i] == System.IO.Path.DirectorySeparatorChar) {
						i++;
						break;
					}
				}
				return path.Substring(i);
			}
		}
	}

	public enum PathArrowButtonState
	{
		Collapsed,
		Expanded
	}

	public class PathArrowButton : ThemedButton
	{
		private string path;
		private DirectoryPicker picker;
		private Func<string, bool> openPath;
		private new PathButtonPresenter Presenter;
		public PathArrowButtonState ArrowState;
		public PathBarButtonState State;
		public PathFolderButton folderButton;

		public PathArrowButton(Func<string, bool> openPath, string path = null) : base()
		{
			this.path = path;
			this.openPath = openPath;
			MinMaxHeight = 20;
			Presenter = new PathButtonPresenter();
			base.Presenter = Presenter;
			if (path == null) {
				Updating += (float delta) => {
					if (ArrowState == PathArrowButtonState.Expanded) {
						State = PathBarButtonState.Press;
					} else {
						if (IsMouseOverThisOrDescendant()) {
							if (WasClicked()) {
								State = PathBarButtonState.Press;
							} else {
								State = PathBarButtonState.Hover;
							}
						} else {
							State = PathBarButtonState.Normal;
						}
					}
					Presenter.SetState(State);
				};
			}
			Gestures.Add(new ClickGesture(0, FlipState));
			Text = ">";
			ArrowState = PathArrowButtonState.Collapsed;
			MinMaxWidth = Renderer.MeasureTextLine(Text, Theme.Metrics.TextHeight, 5).X;
		}

		public void SetState(PathBarButtonState state)
		{
			Presenter.SetState(state);
		}

		private void FlipState()
		{
			if (ArrowState == PathArrowButtonState.Collapsed) {
				Text = "v";
				ArrowState = PathArrowButtonState.Expanded;
				var indent = 14;
				var pickerPosition = Window.Current.LocalToDesktop(GlobalPosition + new Vector2(-indent, Height));
				picker = new DirectoryPicker(openPath, pickerPosition, path);
				
				picker.Window.Deactivated += () => {
					picker.Window.Close();
					FlipState();
				};
			} else {
				Text = ">";
				ArrowState = PathArrowButtonState.Collapsed;
				picker.Window.Close();
			}
		}
	}

	public class DirectoryPicker
	{
		private Func<string, bool> openPath;
		private ThemedScrollView scrollView;
		public Window Window
		{
			get;
		}
		public WindowWidget RootWidget;

		public DirectoryPicker(Func<string, bool> openPath, Vector2 globalPosition, string path = null) : base()
		{
			this.openPath = openPath;

			List<FilesystemItem> filesystemItems = new List<FilesystemItem>();
			if (path == null) {
				var logicalDrives = System.IO.Directory.GetLogicalDrives();
				var availableRoots = GetAvailableRootsPathsFromLogicalDrives(logicalDrives);
				filesystemItems = GetFilesystemItems(availableRoots);
			} else {
				var internalFolders = GetInternalFoldersPaths(path);
				filesystemItems = GetFilesystemItems(internalFolders);
			}

			scrollView = new ThemedScrollView();
			scrollView.Content.Layout = new VBoxLayout();
			scrollView.Content.Padding = new Thickness(5);
			scrollView.Content.Nodes.AddRange(filesystemItems);

			// Like in Windows File Explorer
			const int MaxItemsOnPicker = 19; 
			var itemsCount = System.Math.Min(filesystemItems.Count, MaxItemsOnPicker);
			var clientSize = new Vector2(FilesystemItem.ItemWidth, (FilesystemItem.IconSize + 2 * FilesystemItem.ItemPadding) * itemsCount) + new Vector2(scrollView.Content.Padding.Left * 2);
			scrollView.MinMaxSize = clientSize;

			var windowOptions = new WindowOptions() {
				ClientSize = scrollView.MinSize,
				MinimumDecoratedSize = scrollView.MinSize,
				FixedSize = true,
				Style = WindowStyle.Borderless,
				Centered = globalPosition == Vector2.Zero,
				Visible = false,
			};
			Window = new Window(windowOptions);

			RootWidget = new ThemedInvalidableWindowWidget(Window) {
				Layout = new VBoxLayout(),
				LayoutBasedWindowSize = true,
				Nodes = {
					scrollView
				}
			};
			RootWidget.FocusScope = new KeyboardFocusScope(RootWidget);
			RootWidget.AddChangeWatcher(() => WidgetContext.Current.NodeUnderMouse, (value) => {
				if (value != null)
					if (scrollView.Content == value.Parent) {
					Window.Current.Invalidate();
				}
			});

			RootWidget.Presenter = new DelegatePresenter<Widget>(_ => {
				RootWidget.PrepareRendererState();
				Renderer.DrawRect(Vector2.One, RootWidget.ContentSize, Theme.Colors.DirectoryPickerBackground);
			});
			RootWidget.CompoundPostPresenter.Add(new DelegatePresenter<Widget>(_ => {
				RootWidget.PrepareRendererState();
				Renderer.DrawRectOutline(Vector2.Zero, RootWidget.ContentSize, Theme.Colors.DirectoryPickerOutline, thickness: 2);
			}));

			Window.Visible = true;
			if (globalPosition != Vector2.Zero) {
				Window.ClientPosition = globalPosition;
			}
		}

		public static List<string> GetInternalFoldersPaths(string path)
		{
			var foldersPaths = new List<string>();
			foreach (var item in System.IO.Directory.EnumerateDirectories(path).OrderBy(f => f)) {
				foldersPaths.Add(item);
			}
			return foldersPaths;
		}

		private List<FilesystemItem> GetFilesystemItems(List<string> paths)
		{
			var items = new List<FilesystemItem>();
			foreach (var path in paths) {
				FilesystemItem item;
				items.Add(item = new FilesystemItem(path));
				item.CompoundPresenter.Add(new DelegatePresenter<Widget>(_ => {
					if (item.IsMouseOverThisOrDescendant()) {
						item.PrepareRendererState();
						Renderer.DrawRect(Vector2.Zero, item.Size, Theme.Colors.DirectoryPickerItemHoveredBackground);
					}
				}));
				item.Updating += (float delta) => {
					if (item.Input.WasMouseReleased(0)) {
						Window.Close();
						openPath(item.FilesystemPath);
					} else if (item.Input.WasMouseReleased(1)) {
						SystemShellContextMenu.Instance.Show(item.FilesystemPath);
					}
				};
			}
			return items;
		}

		public static List<string> GetAvailableRootsPathsFromLogicalDrives(string[] logicalDrives)
		{
			var realRootsCount = 0;
			foreach (var path in logicalDrives) {
				if (System.IO.Directory.Exists(path)) {
					realRootsCount++;
				}
			}
			List<string> availableRoots = new List<string>();
			var i = 0;
			foreach (var root in logicalDrives) {
				if (System.IO.Directory.Exists(root)) {
					availableRoots.Add(root);
					i++;
					if (i == realRootsCount) break;
				}
			}
			return availableRoots;
		}
	}
}
