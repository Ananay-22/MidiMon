using System;
using System.Collections;
using System.Collections.Generic;
using ABC;
using ABCUnity;
using UnityEngine;
using UnityEngine.UI;
using AssemblyCSharp;
using com.shephertz.app42.gaming.multiplayer.client;
using com.shephertz.app42.gaming.multiplayer.client.events;
using Minis;
using NewBark;
using NewBark.Input;
using Pokemon;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using NativeWebSocket;

public class SC_BattleManager : MonoBehaviour {
    
    private int currentSelection;
    private int currentMove;
    public List<Sprite> battleBackgrounds;
    public Image Img_battleBG;
    private const float startTime = 30f;
    private float currentTime;

    // states
    private GlobalEnums.Turns currentTurn;
    private GlobalEnums.BattleMenus currentMenu;
    private GlobalEnums.BattleStates battleState;
    private GlobalEnums.MessageBoxState MessageState;

    // bools
    public bool isAbleToPress;
    private bool isMultiplayer;
    private bool isInBattle;
    private bool isWaitingForRespond;
    private bool isSelectionMenuEnabled;
    private bool isMovesMenuEnabled;
    private bool isFoeAttackingATM;
    private bool canExit;

    [Header("Music")] 
    public string fileName;
    public Layout layout;
    public GameObject MusicMenu;
    private bool isMusicInput;

    [Header("Scripts")]
    public SC_GameLogic SC_GameLogic;
    public SC_MenuLogic SC_MenuLogic;
    public SC_DeckMenuLogic SC_DeckMenuLogic;
    public SC_LoadingMenuLogic SC_LoadingMenuLogic;
    private SC_BasePokemon foePokemon;
    private SC_BasePokemon playerPokemon;
    private SC_PokemonMove attackMove;

    [Header("Animations")]
    public Animator backgroundAnimator;
    public Animator playerAnimator;
    public Animator playerBoxAnimator;
    public Animator foeAnimator;
    public Animator foeBoxAnimator;

    [Header("Cameras")]
    public GameObject menuCamera;
    public GameObject battleCamera;

    [Header("Sounds")]
    public AudioSource victoryMusic;
    public AudioSource losingMusic;
    public AudioSource clickSound;
    public AudioSource runSound;

    [Header("Foe")]
    public Image Img_foePokemon;
    public Text Text_pokemonNameFoe;
    public Text Text_pokemonLvlFoe;
    public Text FoeTimeLeft;
    public GameObject foeHealthBar;

    [Header("Player")]
    public Image Img_playerPokemon;
    public Text Text_pokemonNamePlayer;
    public Text Text_pokemonLvlPlayer;
    public Text Text_PlayerHP;
    public Text PlayerTimeLeft;
    public GameObject playerHealthBar;

    [Header("Selection")]
    public GameObject SelectionMenu;
    public Text Fight;
    public Text Run;

    [Header("Message")]
    public GameObject MessageMenu;
    public Text MessageText;

    [Header("Moves")]
    public GameObject MovesMenu;
    public GameObject MovesDetails;
    public Text PPstats;
    public Text PPtype;
    public Text MoveType;
    public Text Move1;
    public Text Move2;
    public Text Move3;
    public Text Move4;

    // Texts
    private string FightTxt;
    private string RunTxt;
    private string Move1Txt;
    private string Move2Txt;
    private string Move3Txt;
    private string Move4Txt;

    private static string menuHendle = "";

    private SongLoader.SongComponent currentComponent;

    [SerializeField] private RectTransform _oppMusicProgress;
    private static RectTransform oppMusicProgress;
    private const int PROGRESS_LOW = 20;
    private const int PROGRESS_HIGH = 2048;
    private static  float oppAccuracy = 0f;
    private static bool finalAccuracyRecv = false;
    [SerializeField] private bool gameTypeLive = true; 
    
    
    [Header("Input Actions")]
    // public InputAction prevSelection;
    // public InputAction nextSelection;
    // public InputAction enterSelection;
    // public InputAction exitSelection;
    // public InputAction leftSelection;
    // public InputAction rightSelection;
    
    

    private InputActionsMaster _controls;
    public InputActionsMaster.PlayerActions Actions => _controls.Player;

    private int correctNotes;
    private int totalNotes;
    private int allNotes;

    private void OnEnable()
    {
        Listener.OnGameStarted += OnGameStarted;
        Listener.OnMoveCompleted += OnMoveCompleted;
        Listener.OnGameStopped += OnGameStopped;
        // prevSelection.Enable();
        // nextSelection.Enable();
        // enterSelection.Enable();
        // exitSelection.Enable();
        // leftSelection.Enable();
        // rightSelection.Enable();
        _controls.Enable();
    }

    private void OnDisable()
    {
        Listener.OnGameStarted -= OnGameStarted;
        Listener.OnMoveCompleted -= OnMoveCompleted;
        Listener.OnGameStopped -= OnGameStopped;
        // prevSelection.Disable();
        // nextSelection.Disable();
        // enterSelection.Disable();
        // exitSelection.Disable();
        // leftSelection.Disable();
        // rightSelection.Disable();
        _controls.Disable();
    }

    private void Awake()
    {
        initText();
        initBattle();
        initMIDIListener();
        initInputs();
    }

    private void Update()
    {
        // WSConnection.DispatchMessageQueue();

        if (isInBattle == false)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Message);
            return;
        }

        manageTimeLeft();
        UpdateSelectionMenu();
        UpdateMovesMenu();
        if (isWaitingForRespond)
        {
            ManageMessageBox(attackMove);
        }
        else
        {
            handleStartOfBattle();

            if (isInBattle && currentTurn == GlobalEnums.Turns.PlayersTurn && MessageState != GlobalEnums.MessageBoxState.Attack)
            {
                ManagePlayersTurn();
            }
            else if (isInBattle && currentTurn == GlobalEnums.Turns.FoesTurn)
            {
                isFoeAttackingATM = true;
                isSelectionMenuEnabled = false;
                isMovesMenuEnabled = false;
                isWaitingForRespond = true;

                if (isMultiplayer == false)
                    AttackRandomly();
                else
                {
                    MessageState = GlobalEnums.MessageBoxState.WaitingForAttack;
                    ManageMessageBox();
                }
            }
        }
        
        // ManageBattleFlow();
        // ValidateNote(new Note(Pitch.C5, Length.Unknown, Accidental.Unspecified));
    }

    #region init

    private void initText()
    {
        FightTxt = Fight.text;
        RunTxt = Run.text;
        Move1Txt = Move1.text;
        Move2Txt = Move2.text;
        Move3Txt = Move3.text;
        Move4Txt = Move4.text;
        PlayerTimeLeft.enabled = false;
        FoeTimeLeft.enabled = false;
    }

    private IEnumerator<Note> _currFileTracker;

    public void initBattle()
    {
        Img_battleBG.sprite = getRandomBackground();
        // StartCoroutine(WSConnection.StartConnection());
        RestartBattle();
        initPlayer();
        initFoe();
    }
    
    private int GetMidiNoteNumber(Note note) {
        Pitch pitch = note.pitch;
        Accidental accidental = note.accidental;
        // Define a mapping from Pitch enum to MIDI note numbers
        int baseMidiNote = pitch switch
        {
            Pitch.A0 => 21, Pitch.B0 => 23, Pitch.C1 => 24, Pitch.D1 => 26, Pitch.E1 => 28, 
            Pitch.F1 => 29, Pitch.G1 => 31, Pitch.A1 => 33, Pitch.B1 => 35, Pitch.C2 => 36,
            Pitch.D2 => 38, Pitch.E2 => 40, Pitch.F2 => 41, Pitch.G2 => 43, Pitch.A2 => 45,
            Pitch.B2 => 47, Pitch.C3 => 48, Pitch.D3 => 50, Pitch.E3 => 52, Pitch.F3 => 53,
            Pitch.G3 => 55, Pitch.A3 => 57, Pitch.B3 => 59, Pitch.C4 => 60, Pitch.D4 => 62,
            Pitch.E4 => 64, Pitch.F4 => 65, Pitch.G4 => 67, Pitch.A4 => 69, Pitch.B4 => 71,
            Pitch.C5 => 72, Pitch.D5 => 74, Pitch.E5 => 76, Pitch.F5 => 77, Pitch.G5 => 79,
            Pitch.A5 => 81, Pitch.B5 => 83, Pitch.C6 => 84, Pitch.D6 => 86, Pitch.E6 => 88,
            Pitch.F6 => 89, Pitch.G6 => 91, Pitch.A6 => 93, Pitch.B6 => 95, Pitch.C7 => 96,
            Pitch.D7 => 98, Pitch.E7 => 100, Pitch.F7 => 101, Pitch.G7 => 103, Pitch.A7 => 105,
            Pitch.B7 => 107, Pitch.C8 => 108,
            _ => throw new ArgumentException("Invalid pitch", nameof(pitch)),
        };

        // Adjust based on accidental
        if (accidental == Accidental.Sharp) {
            baseMidiNote++;  // Sharp raises the pitch by one semitone
        }
        else if (accidental == Accidental.Flat) {
            baseMidiNote--;  // Flat lowers the pitch by one semitone
        }

        return baseMidiNote;
    }

    public static Note ConvertDisplayNameToNote(string displayName)
    {
        if (string.IsNullOrEmpty(displayName) || displayName.Length < 2)
            throw new ArgumentException("Invalid display name format.", nameof(displayName));

        // Extract components from the display name
        char noteLetter = displayName[0]; // First character is the note letter
        string accidental = "";          // Accidental (# or b)
        int octave;

        // Check for accidental
        if (displayName[1] == '#' || displayName[1] == 'b')
        {
            accidental = displayName[1].ToString();
            octave = int.Parse(displayName.Substring(2)); // Rest is the octave
        }
        else
        {
            octave = int.Parse(displayName.Substring(1)); // No accidental, rest is the octave
        }

        // Determine the base pitch enum value
        string pitchName = $"{noteLetter}{octave}";

        Pitch pitchE = Enum.Parse<Pitch>(pitchName);

        // Determine the accidental enum
        Accidental accidentalEnum = accidental switch
        {
            "#" => Accidental.Sharp,
            "b" => Accidental.Flat,
            _ => Accidental.Unspecified,
        };

        // Create and return the Note object
        return new Note(pitchE, Length.Unknown, accidentalEnum);
    }
    
    public InputAction[] GetActions()
    {
        return new[]
        {
            Actions.ButtonA,
            Actions.ButtonB,
            // Actions.ButtonStart,
            // Actions.ButtonSelect,
            Actions.ButtonDirectional
        };
    }

    private void initInputs() {
        _controls = new InputActionsMaster();

        foreach (var action in GetActions())
        {
            // action.started += ctx =>
            //     target.SendMessage("On" + action.name + "Started", ctx, SendMessageOptions.DontRequireReceiver);

            action.performed += ctx =>
                SendMessage("On" + action.name + "Performed", ctx, SendMessageOptions.DontRequireReceiver);

            // action.canceled += ctx =>
            //     target.SendMessage("On" + action.name + "Canceled", ctx, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void onNote(MidiNoteControl note, float _) {
        if (currentMenu != GlobalEnums.BattleMenus.Music) return;
        ValidateNote(note.noteNumber);
    }
    private void initMIDIListener() {
        foreach (var device in InputSystem.devices) {
            if (device is MidiDevice) {
                var midiDevice = device as MidiDevice;
                try {
                    midiDevice.onWillNoteOn -= onNote;
                } catch (System.Exception e) {}
                midiDevice.onWillNoteOn += onNote;
            }
        }
        InputSystem.onDeviceChange += (device, change) =>
        {
            
            if (change != InputDeviceChange.Added) return;

            var midiDevice = device as MidiDevice;
            if (midiDevice == null) return;
            
            // TODO: Add duration? Need metronome for reference!! Can do rests then too!

            midiDevice.onWillNoteOn += onNote;
        };
    }
    private IEnumerable<Note> GetNotes() {
        if (!layout || layout.layouts == null) yield break;
        foreach (var _layout in layout.layouts) {
            if (_layout == null || _layout.scoreLines == null) continue;
            foreach (var scoreLine in _layout.scoreLines) {
                if (scoreLine == null || scoreLine.measures == null) continue;
                foreach (var measure in scoreLine.measures) {
                    if (measure == null || measure.elements == null) continue;
                    foreach (var element in measure.elements) {
                        if (element == null || element.item == null) continue;
                        if (element?.item?.type == Item.Type.Note) yield return element.item as Note;
                    }
                }
            }
        }
    }

    public void ValidateNote(int noteNumber) {
        if (_currFileTracker == null) return;

        var note = _currFileTracker.Current;

        if (note == null) {
            _currFileTracker.MoveNext();
            note = _currFileTracker.Current;
            correctNotes = 0;
            totalNotes = 0;
        }
        if (note == null) return;
        
        Debug.Log($"${GetMidiNoteNumber(note)} tested against ${noteNumber}");
        
        // Process the current note
        var noteObj = layout.gameObjectMap[note.id];
        foreach (var _renderer in noteObj.GetComponentsInChildren<SpriteRenderer>()) {
            // renderer.color = note.accidental != testAgainst.accidental || note.pitch != testAgainst.pitch 
                // ? Color.red : Color.green;
                var hitNote = GetMidiNoteNumber(note) == noteNumber;
                _renderer.color = hitNote ? new Color(0.16f, 0.63f, 0.4f) : new Color(0.69f, 0.16f, 0.1f);
                correctNotes += hitNote ? 1 : 0;
                totalNotes++;
                // WSConnection.sendProgressUpdate(1f * totalNotes / allNotes);
        }

        // Check if this is the last element (MoveNext would return false after this)
        if (!_currFileTracker.MoveNext()) {
            _currFileTracker.Dispose();
            _currFileTracker = null;
            Debug.Log("Metrics: " + correctNotes + ", " +  totalNotes);
            Debug.Log("Move: " + currentMove);
            currentComponent.playerScores.Add(1f * correctNotes / totalNotes);
            float multiplier =  1.5f * correctNotes / totalNotes;
            if (gameTypeLive && (1f * correctNotes / totalNotes < oppAccuracy)) multiplier = 0f;
            // WSConnection.sendFinalAccuracy(1f * correctNotes / totalNotes);
            StartCoroutine(AttackOpponent(playerPokemon, playerPokemon.moves[currentMove - 1], foePokemon, attackMultiplier: multiplier));
        }
    }


    private void RestartBattle()
    {
        isAbleToPress = true;
        isInBattle = false;
        isMultiplayer = false;
        isWaitingForRespond = true;
        isSelectionMenuEnabled = false;
        isMovesMenuEnabled = false;
        isFoeAttackingATM = false;
        canExit = false;
        MessageState = GlobalEnums.MessageBoxState.EnterBattle;
        battleState = GlobalEnums.BattleStates.Start;
        currentMenu = GlobalEnums.BattleMenus.Message;
        currentTurn = GlobalEnums.Turns.PlayersTurn;
        attackMove = null;
        oppMusicProgress = _oppMusicProgress;
        UpdateProgress(0);
        oppAccuracy = 0f;
        oppMusicProgress.gameObject.SetActive(gameTypeLive);
        finalAccuracyRecv = false;
    }

    private IEnumerator StartEnemyAgent() {
        float progress = 0f;
        while (progress < 1f)
        {
            // Call the UpdateProgress method with the current progress value
            UpdateProgress(progress);

            // Increase the progress by 0.125
            progress += 0.125f;

            // Calculate a random deviation for the wait time (±0.1 seconds)
            float deviation = Random.Range(-0.1f, 0.4f);
            float waitTime = 0.25f + deviation;
            waitTime = 1;

            // Wait for the next update time with the calculated deviation
            yield return new WaitForSeconds(waitTime);
        }

        UpdateProgress(1f);
        oppAccuracy = Random.Range(0.75f, 0.1f);
    }
    
    private static void UpdateProgress(float percentVal) {
        var scale = oppMusicProgress.localScale;
        scale.x = PROGRESS_LOW + percentVal * (PROGRESS_HIGH - PROGRESS_LOW);
        oppMusicProgress.localScale = scale;
    }

    private void initMultiplayerBattle()
    {
        initBattle();
        Dictionary<string, object> _toSend = new Dictionary<string, object>();
        int _pokemonID;

        if (SC_DeckMenuLogic.currentCardIndex == -1)
            _pokemonID = SC_GameLogic.getRandomPokemonFromList(SC_GameLogic.allPokemons).ID;
        else
            _pokemonID = SC_GameLogic.allPokemons[SC_DeckMenuLogic.currentCardIndex].ID;

        currentTime = startTime;

        initPlayer(_pokemonID, SC_DeckMenuLogic.currentSliderValue);
        _toSend.Add("firstPokemonID", _pokemonID);
        int _randomIndex = UnityEngine.Random.Range(0, battleBackgrounds.Count);
        Img_battleBG.sprite = battleBackgrounds[_randomIndex];
        _toSend.Add("battleBackgroundIndex", _randomIndex);
        string _send = MiniJSON.Json.Serialize(_toSend);
        WarpClient.GetInstance().sendMove(_send);
    }

    private void initFoe(int _pokemonID = 000, int _pokemonLvl = 000)
    {
        foeAnimator.Rebind();

        if (_pokemonID == 000)
            foePokemon = SC_GameLogic.getRandomPokemonFromList(SC_GameLogic.allPokemons);
        else
            foePokemon = SC_GameLogic.getPokemonByID(_pokemonID);

        Img_foePokemon.sprite = foePokemon.frontImage;
        Text_pokemonNameFoe.text = foePokemon.name;


        if (_pokemonLvl == 000)
            Text_pokemonLvlFoe.text = "Lv" + foePokemon.level.ToString();
        else
            Text_pokemonLvlFoe.text = "Lv" + _pokemonLvl.ToString();

        foePokemon.HpStats.curr = foePokemon.HpStats.max;
        foePokemon.healthBar = foeHealthBar.GetComponent<SC_HealthBar>();
        StartCoroutine(foePokemon.healthBar.SetHealthBarScale(1f, 1f));

        for (int i = 0; i < foePokemon.moves.Count; i++)
        {
            foePokemon.moves[i] = Instantiate(foePokemon.moves[i]);
            foePokemon.moves[i].currPP = foePokemon.moves[i].maxPP;
        }
    }

    private void initPlayer(int _pokemonID = 000, int _pokemonLvl = 000)
    {
        playerAnimator.Rebind();

        if (_pokemonID == 000)
            playerPokemon = SC_GameLogic.getRandomPokemonFromList(SC_GameLogic.allPokemons);
        else
            playerPokemon = SC_GameLogic.getPokemonByID(_pokemonID);

        Img_playerPokemon.sprite = playerPokemon.backImage;
        Text_pokemonNamePlayer.text = playerPokemon.name;

        if (_pokemonLvl == 000)
            Text_pokemonLvlPlayer.text = "Lv" + playerPokemon.level.ToString();
        else
            Text_pokemonLvlPlayer.text = "Lv" + _pokemonLvl.ToString();

        playerPokemon.HpStats.curr = playerPokemon.HpStats.max;
        Text_PlayerHP.text = playerPokemon.HpStats.curr.ToString() + "/" + playerPokemon.HpStats.max.ToString();
        playerPokemon.healthBar = playerHealthBar.GetComponent<SC_HealthBar>();
        StartCoroutine(playerPokemon.healthBar.SetHealthBarScale(1f, 1f));
        Move1Txt = playerPokemon.moves[0].name;
        Move2Txt = playerPokemon.moves[1].name;
        Move3Txt = playerPokemon.moves[2].name;
        Move4Txt = playerPokemon.moves[3].name;

        for (int i = 0; i < playerPokemon.moves.Count; i++)
        {
            playerPokemon.moves[i] = Instantiate(playerPokemon.moves[i]);
            playerPokemon.moves[i].currPP = playerPokemon.moves[i].maxPP;
        }
    }

    public Sprite getRandomBackground(int _index = 000)
    {
        if (_index == 000)
        {
            int _randomIndex = UnityEngine.Random.Range(0, battleBackgrounds.Count);
            return battleBackgrounds[_randomIndex];
        }
        else
            return battleBackgrounds[_index];
    }

    private GlobalEnums.Turns GetRandomTurn()
    {
        int randomNum = UnityEngine.Random.Range(0, 2);
        GlobalEnums.Turns randTurn = (GlobalEnums.Turns)randomNum;
        return randTurn;
    }

    #endregion

    #region menu

    private void UpdateMusicMenu() {
        // layout.LoadFile("sample.abc");
        currentComponent = SongLoader.CurrentSong.GetRandomSongComponent();
        layout.LoadString(currentComponent.SongContent);
        allNotes = 0;
        var noteCounter = GetNotes().GetEnumerator();
        while (noteCounter.MoveNext()) {
            allNotes++;
        }
        noteCounter.Dispose();
        _currFileTracker = GetNotes().GetEnumerator();
        if (gameTypeLive) StartCoroutine(StartEnemyAgent());
    }

    private void UpdateSelectionMenu()
    {
        switch (currentSelection)
        {
            case 1:
                Fight.text = "<b>> </b>" + FightTxt;
                Run.text = RunTxt;
                break;

            case 2:
                Fight.text = FightTxt;
                Run.text = "<b>> </b>" + RunTxt;
                break;
        }
    }

    private void UpdateMoveDetailBox(SC_PokemonMove move)
    {
        PPstats.GetComponent<Text>().text = move.currPP.ToString() + "/" + move.maxPP.ToString();
        PPtype.text = "TYPE/" + move.type.ToString();
    }

    private void UpdateMovesMenu()
    {
        if (currentTurn == GlobalEnums.Turns.PlayersTurn)
        {
            switch (currentMove)
            {
                case 1:
                    UpdateMoveDetailBox(playerPokemon.moves[0]);
                    Move1.text = "<b>> </b>" + Move1Txt;
                    Move2.text = Move2Txt;
                    Move3.text = Move3Txt;
                    Move4.text = Move4Txt;
                    break;

                case 2:
                    UpdateMoveDetailBox(playerPokemon.moves[1]);
                    Move1.text = Move1Txt;
                    Move2.text = "<b>> </b>" + Move2Txt;
                    Move3.text = Move3Txt;
                    Move4.text = Move4Txt;
                    break;

                case 3:
                    UpdateMoveDetailBox(playerPokemon.moves[2]);
                    Move1.text = Move1Txt;
                    Move2.text = Move2Txt;
                    Move3.text = "<b>> </b>" + Move3Txt;
                    Move4.text = Move4Txt;
                    break;

                case 4:
                    UpdateMoveDetailBox(playerPokemon.moves[3]);
                    Move1.text = Move1Txt;
                    Move2.text = Move2Txt;
                    Move3.text = Move3Txt;
                    Move4.text = "<b>> </b>" + Move4Txt;
                    break;
            }
        }
    }

    private void HandleSelectionMenu()
    {
        // if (currentTurn == GlobalEnums.Turns.PlayersTurn)
        // {
        //     if (Input.GetKeyDown(KeyCode.DownArrow))
        //     {
        //         if (currentSelection < 2)
        //             currentSelection++;
        //     }
        //     if (Input.GetKeyDown(KeyCode.UpArrow))
        //     {
        //         if (currentSelection > 1)
        //             currentSelection--;
        //     }
        //     if (Input.GetKeyDown(KeyCode.Z) && MessageState != GlobalEnums.MessageBoxState.EnterBattle)
        //     {
        //         if (currentSelection == 1)
        //         {
        //             ChangeMenu(GlobalEnums.BattleMenus.Moves);
        //             isSelectionMenuEnabled = false;
        //             isMovesMenuEnabled = true;
        //         }
        //         else if (currentSelection == 2)
        //         {
        //             if (isMultiplayer)
        //                 RunFromMultiplayerBattle();
        //             isInBattle = false;
        //             StartCoroutine(BackToMainMenu());
        //             runSound.Play();
        //         }
        //     }
        //
        //     UpdateSelectionMenu();
        // }
    }

    private void HandleMovesMenu()
    {
        // if (currentMenu == GlobalEnums.BattleMenus.Moves && currentTurn == GlobalEnums.Turns.PlayersTurn)
        // {
        //     if (Input.GetKeyDown(KeyCode.DownArrow))
        //     {
        //         if (currentMove == 1)
        //             currentMove = 3;
        //         else if (currentMove == 2)
        //             currentMove = 4;
        //     }
        //     if (Input.GetKeyDown(KeyCode.UpArrow))
        //     {
        //         if (currentMove == 3)
        //             currentMove = 1;
        //         else if (currentMove == 4)
        //             currentMove = 2;
        //     }
        //     if (Input.GetKeyDown(KeyCode.RightArrow))
        //     {
        //         if (currentMove == 1)
        //             currentMove = 2;
        //         else if (currentMove == 3)
        //             currentMove = 4;
        //     }
        //     if (Input.GetKeyDown(KeyCode.LeftArrow))
        //     {
        //         if (currentMove == 2)
        //             currentMove = 1;
        //         else if (currentMove == 4)
        //             currentMove = 3;
        //     }
        //
        //     UpdateMovesMenu();
        //
        //     if (Input.GetKeyDown(KeyCode.Z))
        //     {
        //         if (currentTurn == GlobalEnums.Turns.PlayersTurn)
        //         {
        //             if (isMultiplayer == true)
        //             {
        //                 Dictionary<string, object> _toSend = new Dictionary<string, object>();
        //                 _toSend.Add("attackMoveID", playerPokemon.moves[currentMove - 1].ID);
        //                 string _send = MiniJSON.Json.Serialize(_toSend);
        //                 WarpClient.GetInstance().sendMove(_send);
        //             }
        //
        //             InputMusic();
        //         }
        //
        //         isSelectionMenuEnabled = false;
        //         isMovesMenuEnabled = false;
        //     }
        //
        //     if (Input.GetKeyDown(KeyCode.X))
        //     {
        //         SC_MenuLogic.backClick.Play();
        //         ChangeMenu(GlobalEnums.BattleMenus.Selection);
        //         isSelectionMenuEnabled = true;
        //         isMovesMenuEnabled = false;
        //     }

        // }
    }


    private void ManageMessageBox(SC_PokemonMove _move = null)
    {
        if (MessageState == GlobalEnums.MessageBoxState.EnterBattle)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Message);
            MessageText.text = foePokemon.name + "  Wants  To  Battle!";
        }
        else if (MessageState == GlobalEnums.MessageBoxState.Selection)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Selection);
            MessageText.text = "What  Will  " + playerPokemon.name + "  Do?";
        }
        else if (MessageState == GlobalEnums.MessageBoxState.WaitingForAttack)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Message);
            MessageText.text = "Enemy  Pokemon  Is  Attacking.";
        }
        else if (MessageState == GlobalEnums.MessageBoxState.EnemyRanAway)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Message);
            MessageText.text = "Enemy  " + foePokemon.name + "  Ran  Away...  \nYou  Win!";
            isWaitingForRespond = true;
        }
        else if (MessageState == GlobalEnums.MessageBoxState.Attack)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Message);
            isWaitingForRespond = true;

            if (_move != null)
            {
                if (currentTurn == GlobalEnums.Turns.PlayersTurn)
                    MessageText.text = playerPokemon.name + "  Used  " + _move.name + "! (" + Math.Round(10000f * correctNotes / totalNotes) / 100f + "%)";
                else if (currentTurn == GlobalEnums.Turns.FoesTurn)
                    MessageText.text = "Oh No! \nEnemy  " + foePokemon.name + "  Used  " + _move.name + "!";
            }
        }
        else if (MessageState == GlobalEnums.MessageBoxState.GameOver)
        {
            ChangeMenu(GlobalEnums.BattleMenus.Message);

            if (playerPokemon.HpStats.curr <= 0)
                MessageText.text = playerPokemon.name + "  Defeated!  \nMaybe  Next  Time...";
            else if (foePokemon.HpStats.curr <= 0)
                MessageText.text = "Enemy  " + foePokemon.name + "  Defeated! \nGood  Job!";
        }
    }

    private void ManagePlayersTurn()
    {
        if (currentMenu == GlobalEnums.BattleMenus.Selection && isSelectionMenuEnabled == true)
        {
            isMovesMenuEnabled = false;
            // HandleSelectionMenu();
        }
        else if (currentMenu == GlobalEnums.BattleMenus.Moves && isMovesMenuEnabled == true)
        {
            isSelectionMenuEnabled = false;
            // HandleMovesMenu();
        }
    }

    private void manageTimeLeft()
    {
        if (isMultiplayer)
        {
            currentTime -= 1 * Time.deltaTime;
            if (currentTime <= 0)
                currentTime = 0;

            PlayerTimeLeft.text = currentTime.ToString("0");
            FoeTimeLeft.text = currentTime.ToString("0");
        }
    }

    private void managePlayersClock(MoveEvent _Move)
    {
        if (isMultiplayer)
        {
            if (PlayerTimeLeft.enabled == true)
            {
                FoeTimeLeft.enabled = true;
                PlayerTimeLeft.enabled = false;
            }
            else if (FoeTimeLeft.enabled == true)
            {
                PlayerTimeLeft.enabled = true;
                FoeTimeLeft.enabled = false;
            }
        }
    }

    public void ChangeMenu(GlobalEnums.BattleMenus menu)
    {
        switch (menu)
        {
            case GlobalEnums.BattleMenus.Selection:
                SelectionMenu.gameObject.SetActive(true);
                MessageMenu.gameObject.SetActive(true);
                MovesMenu.gameObject.SetActive(false);
                MovesDetails.gameObject.SetActive(false);
                MusicMenu.gameObject.SetActive(false);
                isMusicInput = false;
                break;

            case GlobalEnums.BattleMenus.Moves:
                SelectionMenu.gameObject.SetActive(false);
                MessageMenu.gameObject.SetActive(false);
                MovesMenu.gameObject.SetActive(true);
                MovesDetails.gameObject.SetActive(true);
                MusicMenu.gameObject.SetActive(false);
                isMusicInput = false;
                break;

            case GlobalEnums.BattleMenus.Message:
                SelectionMenu.gameObject.SetActive(false);
                MessageMenu.gameObject.SetActive(true);
                MovesMenu.gameObject.SetActive(false);
                MovesDetails.gameObject.SetActive(false);
                MusicMenu.gameObject.SetActive(false);
                isMusicInput = false;
                break;
            case GlobalEnums.BattleMenus.Music:
                UpdateMusicMenu();
                SelectionMenu.gameObject.SetActive(false);
                MessageMenu.gameObject.SetActive(false);
                MovesMenu.gameObject.SetActive(false);
                MovesDetails.gameObject.SetActive(false);
                MusicMenu.gameObject.SetActive(true);
                isMusicInput = true;
                break;
        }

        currentSelection = 1;
        if (menu != GlobalEnums.BattleMenus.Music) currentMove = 1;
        isInBattle = true;
        currentMenu = menu;
    }


    private void BackToMainMenu()
    {
        SC_GameLogic.battleMusic.Stop();
        victoryMusic.Stop();
        losingMusic.Stop();
        // menuCamera.SetActive(true);
        battleCamera.SetActive(false);
        // SC_MenuLogic.ChangeScreen(GlobalEnums.MenuScreens.MainMenu);
        // SC_MenuLogic.menuMusic.Play();
        // StartCoroutine(SC_MenuLogic.fadeIn(0.8f));
        
        SceneTransition.JumpBackToGameScene();

        SceneManager.UnloadSceneAsync("SampleScene");

        // yield return new WaitForSeconds(0.5f);
        // SC_MenuLogic.isMenuEnabled = true;
        // SC_MenuLogic.enabled = true;
        // WarpClient.GetInstance().stopGame();
    }

    #endregion

    #region logic

    private void ManageBattleFlow()
    {
        // if (battleCamera.activeInHierarchy == false)
        //     return;
        //
        // if (Input.GetKeyDown(KeyCode.Z) && isAbleToPress == true)
        if (isAbleToPress)
        {
            clickSound.Play();

            if (canExit == true) {
                BackToMainMenu();
                return;
            }

            if ((battleState == GlobalEnums.BattleStates.GameOver && battleCamera.activeInHierarchy == true) || (MessageState == GlobalEnums.MessageBoxState.EnemyRanAway))
            {
                FinishBattle();
                return;
            }
            else isWaitingForRespond = false;

            if (MessageState == GlobalEnums.MessageBoxState.EnterBattle && currentTurn == GlobalEnums.Turns.PlayersTurn)
            {
                MessageState = GlobalEnums.MessageBoxState.Selection;
                isSelectionMenuEnabled = true;
                ChangeMenu(GlobalEnums.BattleMenus.Selection);
                ManageMessageBox();
                return;
            }

            if (MessageState == GlobalEnums.MessageBoxState.Attack && isMultiplayer == false)
            {
                if (currentTurn == GlobalEnums.Turns.PlayersTurn)
                {
                    currentTurn = GlobalEnums.Turns.FoesTurn;
                }
                else if (currentTurn == GlobalEnums.Turns.FoesTurn && isFoeAttackingATM != true)
                {
                    currentTurn = GlobalEnums.Turns.PlayersTurn;
                    MessageState = GlobalEnums.MessageBoxState.Selection;
                    ChangeMenu(GlobalEnums.BattleMenus.Selection);
                    ManageMessageBox();
                    isWaitingForRespond = false;
                    isSelectionMenuEnabled = true;
                    isMovesMenuEnabled = false;
                }

                return;
            }
            else if (MessageState == GlobalEnums.MessageBoxState.Attack && isMultiplayer == true)
            {
                if (currentTurn == GlobalEnums.Turns.FoesTurn && isFoeAttackingATM != true)
                {
                    currentTurn = GlobalEnums.Turns.PlayersTurn;
                    MessageState = GlobalEnums.MessageBoxState.Selection;
                    ChangeMenu(GlobalEnums.BattleMenus.Selection);
                    ManageMessageBox();
                    isWaitingForRespond = false;
                    isSelectionMenuEnabled = true;
                    isMovesMenuEnabled = false;
                }
                else
                {
                    MessageState = GlobalEnums.MessageBoxState.WaitingForAttack;
                    ManageMessageBox();
                }

                return;
            }
        }
    }

    private void handleStartOfBattle()
    {
        if (battleState == GlobalEnums.BattleStates.Start)
        {
            battleState = GlobalEnums.BattleStates.Battling;

            if (isMultiplayer == true)
            {
                if (currentTurn == GlobalEnums.Turns.PlayersTurn)
                {
                    MessageState = GlobalEnums.MessageBoxState.Selection;
                    isSelectionMenuEnabled = true;
                    ChangeMenu(GlobalEnums.BattleMenus.Selection);
                    ManageMessageBox();
                }
                else if (currentTurn == GlobalEnums.Turns.FoesTurn)
                {
                    MessageState = GlobalEnums.MessageBoxState.WaitingForAttack;
                    ManageMessageBox();
                }

                if (isInBattle == true && currentTurn == GlobalEnums.Turns.PlayersTurn && MessageState != GlobalEnums.MessageBoxState.Attack)
                {
                    ManagePlayersTurn();
                }
            }
            else
            {
                currentTurn = GetRandomTurn();
            }
        }
    }

    private void RunFromMultiplayerBattle()
    {
        Dictionary<string, object> _toSend = new Dictionary<string, object>();
        _toSend.Add("runFromBattle", true);
        string _send = MiniJSON.Json.Serialize(_toSend);
        WarpClient.GetInstance().sendMove(_send);
    }

    private void FinishBattle()
    {
        SC_GameLogic.battleMusic.Stop();
        if (attackMove != null)
            attackMove.moveSound.Stop();

        if (currentTurn == GlobalEnums.Turns.PlayersTurn)
        {
            foeAnimator.SetTrigger("DieFoe");
            victoryMusic.Play();
        }
        else if (currentTurn == GlobalEnums.Turns.FoesTurn)
        {
            playerAnimator.SetTrigger("DiePlayer");
            losingMusic.Play();
        }

        FoeTimeLeft.enabled = false;
        PlayerTimeLeft.enabled = false;
        isInBattle = false;
        isWaitingForRespond = true;
        MessageState = GlobalEnums.MessageBoxState.GameOver;
        battleState = GlobalEnums.BattleStates.GameOver;
        ManageMessageBox();
        canExit = true;
    }

    private void AttackRandomly()
    {
        int randomIndex = UnityEngine.Random.Range(0, 4);
        float multiplier =  1f * correctNotes / totalNotes;
        if (gameTypeLive && (1f * correctNotes / totalNotes > oppAccuracy)) multiplier = 0f;
        StartCoroutine(AttackOpponent(foePokemon, foePokemon.moves[randomIndex], playerPokemon, attackMultiplier: multiplier));
        isFoeAttackingATM = false;
    }

    private IEnumerator AttackOpponent(SC_BasePokemon _attackPokemon, SC_PokemonMove _attackMove, SC_BasePokemon _defensePokemon, float attackMultiplier = 1.0f)
    {
        if (gameTypeLive && isInBattle && currentTurn == GlobalEnums.Turns.PlayersTurn) {
            while (!finalAccuracyRecv) yield return new WaitForUpdate();
        }
        isAbleToPress = false;
        if (canExit != true)
            _attackMove.moveSound.Play();
        ChangeMenu(GlobalEnums.BattleMenus.Message);
        MessageState = GlobalEnums.MessageBoxState.Attack;
        attackMove = _attackMove;
        ManageMessageBox(attackMove);
        isWaitingForRespond = true;

        _attackMove.currPP--;

        float _lvl = (float)_attackPokemon.level;
        float _power = (float)attackMove.power;
        float _A = (float)_attackPokemon.attack;
        float _D = (float)_defensePokemon.defense;
        float _damage = (((((((2f * _lvl) / 5f) + 2f) * _power) * (_A / _D)) + 2f) / 50f) * attackMultiplier;

        if (currentTurn == GlobalEnums.Turns.PlayersTurn)
        {
            playerAnimator.SetTrigger("Attack");
            backgroundAnimator.SetTrigger("Attack");
            StartCoroutine(FlashAfterAttack(Img_foePokemon, 4, 0.1f));
        }
        else if (currentTurn == GlobalEnums.Turns.FoesTurn)
        {
            playerBoxAnimator.SetTrigger("Attack");
            foeAnimator.SetTrigger("Attack");
            foeBoxAnimator.SetTrigger("Attack");
            backgroundAnimator.SetTrigger("Attack");
            StartCoroutine(FlashAfterAttack(Img_playerPokemon, 4, 0.1f));
        }

        if (_defensePokemon.HpStats.curr - _damage < 1)
        {
            PlayerTimeLeft.enabled = false;
            FoeTimeLeft.enabled = false;
            float _oldHP = (_defensePokemon.HpStats.curr) / _defensePokemon.HpStats.max;
            StartCoroutine(handlePlayerHpDecrease(_defensePokemon, Text_PlayerHP, _defensePokemon.HpStats.curr, 0, 0.06f));
            StartCoroutine(_defensePokemon.healthBar.SetHealthBarScale(_oldHP, 0));
            _defensePokemon.HpStats.curr = 0;
            battleState = GlobalEnums.BattleStates.GameOver;
        }
        else
        {
            float _newHP = (_defensePokemon.HpStats.curr - _damage) / _defensePokemon.HpStats.max;
            float _oldHP = (_defensePokemon.HpStats.curr) / _defensePokemon.HpStats.max;
            StartCoroutine(handlePlayerHpDecrease(_defensePokemon, Text_PlayerHP, _defensePokemon.HpStats.curr, _defensePokemon.HpStats.curr - _damage, 0.06f));
            StartCoroutine(_defensePokemon.healthBar.SetHealthBarScale(_oldHP, _newHP));
            _defensePokemon.HpStats.curr -= _damage;
        }
    }

    private IEnumerator handlePlayerHpDecrease(SC_BasePokemon _pokemon, Text _pokemonHP, float _oldHP, float _newHP, float _delay)
    {
        yield return new WaitForSeconds(_delay);
        while (_oldHP >= _newHP)
        {
            _oldHP--;
            if (currentTurn == GlobalEnums.Turns.FoesTurn)
                _pokemonHP.text = ((int)_oldHP).ToString() + "/" + _pokemon.HpStats.max.ToString();
            yield return new WaitForSeconds(_delay);
        }
        isAbleToPress = true;
    }

    private IEnumerator FlashAfterAttack(Image pokemon, int numOfTimes, float delay)
    {
        yield return new WaitForSeconds(delay);
        for (int i = 0; i < numOfTimes; i++)
        {
            pokemon.color = new Color(pokemon.color.r, pokemon.color.g, pokemon.color.b, 0.1f);
            yield return new WaitForSeconds(delay);
            pokemon.color = new Color(pokemon.color.r, pokemon.color.g, pokemon.color.b, 1);
            yield return new WaitForSeconds(delay);
        }
    }

    #endregion

    #region Events
    private void OnGameStarted(string _Sender, string _RoomId, string _NextTurn)
    {
        if (SC_MenuLogic.Instance.userId == _NextTurn)
        {
            currentTurn = GlobalEnums.Turns.PlayersTurn;
            initMultiplayerBattle();
        }
        else
        {
            currentTurn = GlobalEnums.Turns.FoesTurn;
        }
    }

    private void OnMoveCompleted(MoveEvent _Move)
    {
        Dictionary<string, object> _data = (Dictionary<string, object>)MiniJSON.Json.Deserialize(_Move.getMoveData());
        currentTime = startTime;

        managePlayersClock(_Move);

        if (_data != null && _data.ContainsKey("firstPokemonID") && _data.ContainsKey("battleBackgroundIndex") && _Move.getSender() != SC_MenuLogic.Instance.userId)
        {
            initBattle();
            Img_battleBG.sprite = battleBackgrounds[int.Parse(_data["battleBackgroundIndex"].ToString())];

            if (SC_DeckMenuLogic.currentCardIndex == -1)
                initPlayer(000, SC_DeckMenuLogic.currentSliderValue);
            else
                initPlayer(SC_GameLogic.allPokemons[SC_DeckMenuLogic.currentCardIndex].ID, SC_DeckMenuLogic.currentSliderValue);

            initFoe(int.Parse(_data["firstPokemonID"].ToString()), SC_DeckMenuLogic.currentSliderValue);

            Dictionary<string, object> _toSend = new Dictionary<string, object>();
            _toSend.Add("secondPokemonID", playerPokemon.ID);
            string _send = MiniJSON.Json.Serialize(_toSend);
            WarpClient.GetInstance().sendMove(_send);
        }
        else if (_data != null && _data.ContainsKey("secondPokemonID") && _Move.getSender() != SC_MenuLogic.Instance.userId)
        {
            initFoe(int.Parse(_data["secondPokemonID"].ToString()), SC_DeckMenuLogic.currentSliderValue);

            Dictionary<string, object> _toSend = new Dictionary<string, object>();
            _toSend.Add("startBattle", true);
            string _send = MiniJSON.Json.Serialize(_toSend);
            WarpClient.GetInstance().sendMove(_send);
        }
        else if (_data != null && _data.ContainsKey("startBattle"))
        { 
            SC_GameLogic.EnterBattle(true);
            isInBattle = true;
            isMultiplayer = true;

            if (_Move.getSender() != SC_MenuLogic.Instance.userId)
            {
                PlayerTimeLeft.enabled = true;
                FoeTimeLeft.enabled = false;
            }
            else if (_Move.getSender() == SC_MenuLogic.Instance.userId)
            {
                FoeTimeLeft.enabled = true;
                PlayerTimeLeft.enabled = false;
            }
        }

        if (_data != null && _data.ContainsKey("attackMoveID") && _Move.getSender() != SC_MenuLogic.Instance.userId)
        {
            int _attackMoveID = int.Parse(_data["attackMoveID"].ToString());
            currentTurn = GlobalEnums.Turns.FoesTurn;
            isFoeAttackingATM = true;
            isSelectionMenuEnabled = false;
            isMovesMenuEnabled = false;
            isWaitingForRespond = true;
            float multiplier =  1f * correctNotes / totalNotes;
            if (gameTypeLive && (1f * correctNotes / totalNotes > oppAccuracy)) multiplier = 0f;
            StartCoroutine(AttackOpponent(foePokemon, SC_GameLogic.getMoveByID(_attackMoveID), playerPokemon, attackMultiplier:multiplier));
            isFoeAttackingATM = false;
            return;
        }
        else if (_data != null && _data.ContainsKey("attackMoveID") && _Move.getSender() == SC_MenuLogic.Instance.userId)
        {
            return;
        }

        handleRunnigFromBattle(_Move, _data);
        handleLostOfTime(_Move, _data);

        switchTurns(_Move);
    }

    private void handleRunnigFromBattle(MoveEvent _Move, Dictionary<string, object> _data)
    {
        if (_data != null && _data.ContainsKey("runFromBattle") && _Move.getSender() != SC_MenuLogic.Instance.userId)
        {
            MessageState = GlobalEnums.MessageBoxState.EnemyRanAway;
            ManageMessageBox();
        }
    }

    private void handleLostOfTime(MoveEvent _Move, Dictionary<string, object> _data)
    {
        if (_data == null && _Move.getSender() != SC_MenuLogic.Instance.userId)
        {
            MessageState = GlobalEnums.MessageBoxState.Selection;
            isSelectionMenuEnabled = true;
            isWaitingForRespond = false;
            ManageMessageBox();
            ManagePlayersTurn();
        }
        else if (_data == null && _Move.getSender() == SC_MenuLogic.Instance.userId)
        {
            MessageState = GlobalEnums.MessageBoxState.WaitingForAttack;
            ManageMessageBox();
        }
    }

    private void switchTurns(MoveEvent _Move)
    {
        if (_Move.getNextTurn() == SC_MenuLogic.Instance.userId)
            currentTurn = GlobalEnums.Turns.PlayersTurn;
        else
            currentTurn = GlobalEnums.Turns.FoesTurn;
    }

    private void OnGameStopped(string _Sender, string _RoomId)
    {
        Debug.Log("Game Stopped");
        WarpClient.GetInstance().LeaveRoom(_RoomId);
        SC_LoadingMenuLogic.canUserCancel = true;
    }
    #endregion
    
    #region InputCallbacks
    
    //TODO: make sure no clash with the gameplay during music mode
    void OnPrevSelection() {
        
        if (currentTurn == GlobalEnums.Turns.PlayersTurn) {
            if (currentMenu == GlobalEnums.BattleMenus.Selection && currentSelection > 1)
                currentSelection--;

            else if (currentMenu == GlobalEnums.BattleMenus.Moves) {
                
                if (currentMove == 1)
                    currentMove = 3;
                else if (currentMove == 2)
                    currentMove = 4;
            }
        }
    }
    void OnNextSelection() {
        if (currentTurn == GlobalEnums.Turns.PlayersTurn) {
            if (currentMenu == GlobalEnums.BattleMenus.Selection && currentSelection < 2)
                currentSelection++;

            else if (currentMenu == GlobalEnums.BattleMenus.Moves) {
                if (currentMove == 3)
                    currentMove = 1;
                else if (currentMove == 4)
                    currentMove = 2;
            }
        }
    }
    void OnButtonAPerformed() {
        if (isMusicInput) return;
        // if (currentMenu != GlobalEnums.BattleMenus.Message ||
        //     MessageState != GlobalEnums.MessageBoxState.EnterBattle) return;
        if (currentTurn == GlobalEnums.Turns.PlayersTurn) {
            if (currentMenu == GlobalEnums.BattleMenus.Selection) {
                if (currentSelection == 1) {
                    ChangeMenu(GlobalEnums.BattleMenus.Moves);
                    isSelectionMenuEnabled = false;
                    isMovesMenuEnabled = true;
                }
                else if (currentSelection == 2) {
                    if (isMultiplayer)
                        RunFromMultiplayerBattle();
                    isInBattle = false;
                    runSound.Play();
                    BackToMainMenu();
                    return;
                }
            }
            
            else if (currentMenu == GlobalEnums.BattleMenus.Moves)
            {
                if (isMultiplayer == true)
                {
                    Dictionary<string, object> _toSend = new Dictionary<string, object>();
                    _toSend.Add("attackMoveID", playerPokemon.moves[currentMove - 1].ID);
                    string _send = MiniJSON.Json.Serialize(_toSend);
                    WarpClient.GetInstance().sendMove(_send);
                }

                isSelectionMenuEnabled = false;
                isMovesMenuEnabled = false;

                ChangeMenu(GlobalEnums.BattleMenus.Music);
            }
        }
        
        ManageBattleFlow();
    }
    void OnButtonBPerformed() {
        if (isMusicInput) return;
        if (currentMenu == GlobalEnums.BattleMenus.Moves)
        {
            SC_MenuLogic.backClick.Play();
            ChangeMenu(GlobalEnums.BattleMenus.Selection);
            isSelectionMenuEnabled = true;
            isMovesMenuEnabled = false;
        }
    }
    void OnRightSelection() {
        if (currentTurn == GlobalEnums.Turns.PlayersTurn && currentMenu == GlobalEnums.BattleMenus.Moves) {
            if (currentMove == 1)
                currentMove = 2;
            else if (currentMove == 3)
                currentMove = 4;
        }
    }
    void OnLeftSelection() {
        if (currentTurn == GlobalEnums.Turns.PlayersTurn && currentMenu == GlobalEnums.BattleMenus.Moves) {
            if (currentMove == 2)
                currentMove = 1;
            else if (currentMove == 4)
                currentMove = 3;
        }
    }

    void OnButtonDirectionalPerformed(InputAction.CallbackContext context) {
        if (isMusicInput) return;
        var input = context.ReadValue<Vector2>();
        if (input.x < -0.5f)  // Left
        {
            OnLeftSelection();
        }
        else if (input.x > 0.5f)  // Right
        {
            OnRightSelection();
        }
        else if (input.y > 0.5f)  // Down
        {      
            OnNextSelection();  
        }
        else if (input.y < -0.5f)  // Up
        { 
            OnPrevSelection();   
        }
    }
    
    #endregion

    // private static class WSConnection
    // {
    //     static WebSocket websocket = new("ws://localhost:8080");
    //
    //     // Start is called before the first frame update
    //     public static IEnumerator StartConnection() {
    //         if (websocket.State != WebSocketState.Open) {
    //             websocket.OnOpen += () => { Debug.Log("Connection Open"); };
    //             websocket.OnMessage += (bytes) => {
    //                 Debug.Log("recv: " + System.Text.Encoding.UTF8.GetString(bytes));
    //                 receiveString(System.Text.Encoding.UTF8.GetString(bytes));
    //             };
    //             yield return websocket.Connect();
    //         }
    //     }
    //
    //     public static void sendProgressUpdate(float progress) {
    //         sendString($"p{progress}");
    //     }
    //
    //     public static void sendFinalAccuracy(float accuracy) {
    //         sendString($"a{accuracy}");
    //     }
    //
    //     static void sendString(string message) {
    //         websocket.SendText(message);
    //     }
    //
    //     public static void DispatchMessageQueue() {
    //         websocket.DispatchMessageQueue();
    //     }
    //
    //     static void receiveProgressUpdate(float progress) {
    //         UpdateProgress(progress);
    //     }
    //
    //     static void receiveFinalAccuracy(float accuracy) {
    //         finalAccuracyRecv = true;
    //         oppAccuracy = accuracy;
    //     }
    //
    //     static void receiveString(string message) {
    //         if (message[0] == 'p') {
    //             receiveProgressUpdate(float.Parse(message.Substring(1)));
    //         } else if (message[0] == 'a') {
    //             receiveFinalAccuracy(float.Parse(message.Substring(1)));
    //         }
    //     }
    //
    //     static void WSConnection_Dtor(object sender, System.EventArgs e) {
    //         websocket.Close();
    //     }
    //     
    //     private sealed class Destructor {
    //         ~Destructor() {
    //             websocket.Close();
    //         }
    //     }
    // }
}
