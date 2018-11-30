using System;
using Lime;
using Tangerine.Core;
using Tangerine.Core.ExpressionParser;

namespace Tangerine.UI
{
	public class IntPropertyEditor : CommonPropertyEditor<int>
	{
		private NumericEditBox editor;

		public IntPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			editor = editorParams.NumericEditBoxFactory();
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			EditorContainer.AddNode(editor);
			EditorContainer.AddNode(Spacer.HStretch());
			var current = CoalescedPropertyValue();
			editor.Submitted += text => SetComponent(text, current.GetValue());
			editor.AddChangeWatcher(current, v => editor.Text = v.IsUndefined ? v.Value.ToString() : ManyValuesText);
		}

		public void SetComponent(string text, CoalescedValue<int> current)
		{
			if (Parser.TryParse(text, out double newValue) &&
			    PropertyValidator.TryValidateValue((int)newValue, EditorParams.PropertyInfo)) {
				SetProperty((int)newValue);
			}
			editor.Text = current.IsUndefined ? current.Value.ToString() : ManyValuesText;
		}

		public override void Submit()
		{
			var current = CoalescedPropertyValue();
			SetComponent(editor.Text, current.GetValue());
		}
	}
}
