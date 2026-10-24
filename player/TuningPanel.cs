using System.Globalization;
using Godot;

/// <summary>
/// Sliders for tuning the first-person settings while playing. F1 shows and hides the panel, and Save writes the
/// values back into the settings resource so they become the new defaults.
/// </summary>
public partial class TuningPanel : PanelContainer
{
	private static readonly (string Property, string Label, double Min, double Max, double Step)[] Sliders =
	{
		(nameof(FirstPersonSettings.FieldOfView), "Field of view", 60, 120, 1),
		(nameof(FirstPersonSettings.MouseSensitivity), "Mouse sensitivity", 0.01, 0.5, 0.005),
		(nameof(FirstPersonSettings.EyeHeight), "Eye height", 1.4, 1.9, 0.01),
		(nameof(FirstPersonSettings.WalkSpeed), "Walk speed", 1, 5, 0.1),
		(nameof(FirstPersonSettings.SprintSpeed), "Sprint speed", 2, 9, 0.1),
		(nameof(FirstPersonSettings.CrouchSpeed), "Crouch speed", 0.5, 3, 0.1),
		(nameof(FirstPersonSettings.Acceleration), "Acceleration", 2, 60, 1),
		(nameof(FirstPersonSettings.BrakingDeceleration), "Braking", 2, 60, 1),
		(nameof(FirstPersonSettings.AirControl), "Air control", 0, 1, 0.05),
		(nameof(FirstPersonSettings.StrideLength), "Stride length", 0.6, 2.5, 0.05),
		(nameof(FirstPersonSettings.BobHeight), "Head bob", 0, 0.1, 0.005),
		(nameof(FirstPersonSettings.SprintFovBoost), "Sprint FOV boost", 0, 15, 0.5),
	};

	[Export] public FirstPersonSettings Settings { get; set; }

	public override void _Ready()
	{
		Visible = false;
		var column = new VBoxContainer();
		column.AddChild(new Label { Text = "First-person tuning (F1)" });

		var grid = new GridContainer { Columns = 3 };
		foreach ((string property, string label, double min, double max, double step) in Sliders)
			AddSlider(grid, property, label, min, max, step);
		column.AddChild(grid);

		var save = new Button { Text = "Save as defaults" };
		save.Pressed += () => save.Text = ResourceSaver.Save(Settings) == Error.Ok ? "Saved" : "Could not save";
		column.AddChild(save);
		AddChild(column);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("toggle_tuning"))
			return;

		Visible = !Visible;
		// The mouse is needed for the sliders while the panel is open.
		Input.MouseMode = Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		GetViewport().SetInputAsHandled();
	}

	private void AddSlider(GridContainer grid, string property, string label, double min, double max, double step)
	{
		var value = new Label { CustomMinimumSize = new Vector2(56f, 0f) };
		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = (double)Settings.Get(property),
			CustomMinimumSize = new Vector2(220f, 0f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		slider.ValueChanged += newValue =>
		{
			Settings.Set(property, newValue);
			value.Text = Format(newValue);
		};
		value.Text = Format(slider.Value);

		grid.AddChild(new Label { Text = label });
		grid.AddChild(slider);
		grid.AddChild(value);
	}

	private static string Format(double value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}
}
