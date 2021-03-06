﻿using System.Collections.Generic;
using UnityEngine;
using Model;
using UnityEngine.UI;

public class UnitPlacer : MonoBehaviour {

	public GameController GameController;
	public Player Player;
	private MeshRenderer[] _renderers;
	public int selectedTile;
	public int maxTiles;
	[FMODUnity.EventRef]
	public string confirmSound = "event:/DoubleClick";
	public string selectSound = "event:/Click";

	public PreviewTile PreviewTile;

	public Image[] BackGroudImage;

	/// <summary>
	/// Key bindings to select the units in the array.
	/// </summary>
	public KeyCode[] UnitSelectionKeys = {
		KeyCode.Alpha1,
		KeyCode.Alpha2,
		KeyCode.Alpha3,
		KeyCode.Alpha4,
	};

	public KeyCode MoveNorthKey 				= KeyCode.W; 
	public KeyCode MoveNortheastKey				= KeyCode.None; 
	public KeyCode MoveSoutheastKey				= KeyCode.D; 	
	public KeyCode MoveSouthKey					= KeyCode.S; 
	public KeyCode MoveSouthwestKey				= KeyCode.A; 
	public KeyCode MoveNorthwestKey				= KeyCode.None; 
	
	public KeyCode RotateAnticlockwiseKey		= KeyCode.Z;
	public KeyCode RotateClockwiseKey			= KeyCode.X;
	
	public KeyCode MirrorToggleKey				= KeyCode.Q;

	public KeyCode DeployKey					= KeyCode.E;

	private GameObject[] Units	// shorthand alias
	{
		get { return Player.Units; }
	}

	private Color _c;
	private Transform _t;
	private GameObject _preview;
	private PreviewTile _previewTile;
	private List<PreviewTile> _pathPreview; 		
	
	private int _selectedUnit 					= -1;
	private TileVector _selectedPos 			= new TileVector(0, 0);
	private CardinalDirection _selectedDir 		= CardinalDirection.North;
	private bool _selectedMirrored;



	// Use this for initialization
	void Start () 
	{
		_t = GetComponent<Transform>();
		_pathPreview = new List<PreviewTile>();
		_selectedMirrored = Player.MirrorDefault;
		_previewTile = Instantiate(PreviewTile, _t);
		_c = Player.Color;
		_c.a = 0.7f;
		_previewTile.Paint(_c);		
		selectedTile = 0;
		maxTiles = 0;
		
		GameController.OnRoundTimeOut.AddListener(PlaceUnit);	// attempt to place unit at end of round
	}


	
	// Update is called once per frame
	void Update () 
	{
		if (maxTiles == 0) {
			_selectedPos = Player.PlayerPlacables [selectedTile];
			this.transform.position = Player.PlayerPlacables[selectedTile].ToVector3 ();
			maxTiles = Player.PlayerPlacables.Count - 1;
		}
		// MOVEMENT

		// if (Input.GetKeyDown(MoveNorthKey)) 			MovePos(CardinalDirection.North);
		// if (Input.GetKeyDown(MoveNortheastKey)) 		MovePos(CardinalDirection.Northeast);
		// if (Input.GetKeyDown(MoveSoutheastKey)) 		MovePos(CardinalDirection.Southeast);
		// if (Input.GetKeyDown(MoveSouthKey)) 			MovePos(CardinalDirection.South);
		// if (Input.GetKeyDown(MoveSouthwestKey)) 		MovePos(CardinalDirection.Southwest);
		// if (Input.GetKeyDown(MoveNorthwestKey)) 		MovePos(CardinalDirection.Northwest);

		if (Input.GetKeyDown(MoveNorthKey)) 	DirectionSelected(CardinalDirection.North);
		if (Input.GetKeyDown(MoveSouthKey)) 	DirectionSelected(CardinalDirection.South);

		if (Input.GetKeyDown(RotateAnticlockwiseKey)) 	RotateDir(RelativeDirection.ForwardLeft);
		if (Input.GetKeyDown(RotateClockwiseKey)) 	  	RotateDir(RelativeDirection.ForwardRight);

		if (Input.GetKeyDown(MirrorToggleKey)) 			ToggleMirror();

		if (Input.GetKeyDown (DeployKey))				PlaceUnit();


		// UNIT SELECTION / PLACEMENT
		for (var i = 0; i < UnitSelectionKeys.Length; i++)
		{
			if (Input.GetKeyDown(UnitSelectionKeys[i]))
			{
				for (int j = 0; j < UnitSelectionKeys.Length; j++) {
					BackGroudImage [j].color = new Color32 (205, 205, 205, 225);
				}
				BackGroudImage [i].color =_c;
				SelectUnit(i);
				break;
			}

		}

	}

	private void PlaceUnit() {
		if (_selectedUnit <= -1) return;

		if (GameController.MakeUnit(Units[_selectedUnit], Player, _selectedPos, _selectedDir, _selectedMirrored))
		{
			BackGroudImage [_selectedUnit].color = new Color32 (205, 205, 205, 225);
			FMODUnity.RuntimeManager.PlayOneShot (confirmSound, new Vector3(0,0,0));
			SelectUnit(-1);
		}
	}

	private void SelectUnit(int index) 
	{
		if (_selectedUnit >= 0)
		{
			var inverseDirectionHint = SelectedAvatar().Ai.PreviewDirectionHint().Mirror();
			RotateDir(_selectedMirrored ? inverseDirectionHint.Mirror() : inverseDirectionHint);
		}
		
		_selectedUnit = index;
		if (_preview != null) Destroy(_preview);
		if (index >= 0)
		{
			_preview = Instantiate(Units[index], _t, false);
			_preview.transform.localPosition = Vector3.zero;
			_preview.transform.localRotation = Quaternion.identity;

			var color = Player.Color;
			color.a = 0.5f;
			_preview.GetComponent<UnitAvatar>().Paint(color);
			
			var directionHint = SelectedAvatar().Ai.PreviewDirectionHint();
			RotateDir(_selectedMirrored ? directionHint.Mirror() : directionHint);
			//FMODUnity.RuntimeManager.PlayOneShot (selectSound, new Vector3(0,0,0));
		}
		UpdatePathPreview();
	}
	
	private void UpdatePathPreview()
	{
		for (var i = _pathPreview.Count - 1; i >= 0; i--)		// clean up old preview
		{	
			Destroy(_pathPreview[i].gameObject);
			_pathPreview.RemoveAt(i);
		}

		if (_selectedUnit >= 0)									// build new one
		{
			var avatar = SelectedAvatar();
			var unit = avatar.CreateUnit(Player, _selectedPos, _selectedDir, _selectedMirrored);	// need for ai plan 
			var ai = avatar.Ai;
			var color = Player.Color;
			
			foreach (var step in ai.GetPreview(unit, GameController.World))
			{
				var hex = GameController.World[step.Pos];
				if (hex == null || hex.Impassable)
				{
					color = Color.gray;
				}
					
				var highlight = Instantiate(PreviewTile, _t, true);
				highlight.transform.position = step.Pos.ToVector3();

				color.a = 1.0f / (2 + step.Index * 2);
				highlight.Paint(color);
				
				_pathPreview.Add(highlight);
			}
		}
	}

	private UnitAvatar SelectedAvatar()
	{
		if (_selectedUnit <= -1) return null;
		else return Units[_selectedUnit].GetComponent<UnitAvatar>();
	}

	// private void MovePos(CardinalDirection direction) {
		// _selectedPos = _selectedPos + direction;
		// _t.position = _selectedPos.ToVector3();
		// UpdatePathPreview();
	// }
		
	private void DirectionSelected(CardinalDirection direction) {
		FMODUnity.RuntimeManager.PlayOneShot (selectSound, new Vector3(0,0,0));
		if (direction == CardinalDirection.North) {
			if (selectedTile == 0) {
				selectedTile = maxTiles;
			} else {
				selectedTile = selectedTile - 1;
			}
		}
		else if (direction == CardinalDirection.South) {
			
			if (selectedTile == maxTiles) {
				selectedTile = 0;
			} else {
				selectedTile = selectedTile + 1;
			}
		}
		this.transform.position = Player.PlayerPlacables[selectedTile].ToVector3 ();
		_selectedPos = Player.PlayerPlacables [selectedTile];
		UpdatePathPreview();
	}

	private void RotateDir(RelativeDirection direction)
	{
		_selectedDir = _selectedDir.Turn(direction);
		_t.rotation = _selectedDir.GetBearingRotation();
		FMODUnity.RuntimeManager.PlayOneShot (selectSound, new Vector3(0,0,0));
		UpdatePathPreview();
	}
	
	private void ToggleMirror() 
	{
		if (_selectedUnit >= 0)
		{
			var mirrorHint = SelectedAvatar().Ai.PreviewMirrorHint();
			RotateDir(_selectedMirrored ? mirrorHint.Mirror() : mirrorHint);
		}
		_selectedMirrored = !_selectedMirrored;
		UpdatePathPreview();
	}

	private void OnValidate()
	{
		if (Player == null || UnitSelectionKeys == null) return;
		if (UnitSelectionKeys.Length < Units.Length)
		{
			Debug.LogWarning(
				string.Format("UnitPlacer {0} does not have enough key binds for Player {1}'s Loadout.", this, Player)
			);
		}
	}
}
