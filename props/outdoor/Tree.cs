using Godot;

/// <summary>
/// A tree built from a scanned model. Its meshes fade out far away, where the fog hides them anyway,
/// and trees that only fill the background can skip casting shadows.
/// </summary>
public partial class Tree : StaticBody3D
{
	[Export] public float FadeOutDistance { get; set; } = 90f;
	[Export] public bool CastsShadow { get; set; } = true;

	public override void _Ready()
	{
		foreach (Node node in FindChildren("*", nameof(GeometryInstance3D), true, false))
		{
			var geometry = (GeometryInstance3D)node;
			geometry.VisibilityRangeEnd = FadeOutDistance;
			geometry.VisibilityRangeEndMargin = 10f;
			geometry.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
			if (!CastsShadow)
				geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}
	}
}
