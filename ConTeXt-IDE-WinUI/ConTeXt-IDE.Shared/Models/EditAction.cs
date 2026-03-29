namespace CodeEditorControl_WinUI
{
    public enum EditActionType
    {
        Add,
        Paste,
        Delete,
        Replace,
        Tab,
        Backspace,
        ToggleComment,
        Other
    }

    public class EditAction
    {
        public EditActionType EditActionType { get; set; }
        public string TextInvolved { get; set; }
        public Range Selection { get; set; }

        public override string ToString()
        {
            return TextInvolved;
        }
    }
}
