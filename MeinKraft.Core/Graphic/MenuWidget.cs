public class MenuWidget
{
    public MenuWidget()
    {
        Visible = true;
        FontSize = 14;
        NextWidget = -1;
        HasKeyboardFocus = false;
    }

    public void GetFocus()
    {
        HasKeyboardFocus = true;
        if (Type == UIWidgetType.Textbox)
        {
            Editing = true;
        }
    }

    public void LoseFocus()
    {
        HasKeyboardFocus = false;
        if (Type == UIWidgetType.Textbox)
        {
            Editing = false;
        }
    }

    public string Text { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Sizex { get; set; }
    public float Sizey { get; set; }
    public bool Pressed { get; set; }
    public bool Hover { get; set; }
    public UIWidgetType Type { get; set; }
    public bool Editing { get; set; }
    public bool Visible { get; set; }
    public float FontSize { get; set; }
    public string Description { get; set; }
    public bool Password { get; set; }
    public bool Selected { get; set; }
    public ButtonStyle ButtonStyle { get; set; }
    public string Image { get; set; }
    public int NextWidget { get; set; }
    public bool HasKeyboardFocus { get; set; }
    public int Color { get; set; }
    public string Id { get; set; }
    public bool Isbutton { get; set; }
    public TextFont Font { get; set; }
}