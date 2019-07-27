using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    enum Judge { Good, Miss };
    private readonly KeyCode[] keys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };

    [SerializeField]
    private Transform[] lanes;

    [SerializeField]
    private Note notePrefab;

    private List<Note> notes = new List<Note>();
    public TextAsset fumenFile;
    private const float judgeRange = 0.1f;
    private const float showNoteTimeRangeSec = 0.75f; //ノーツが画面中に見える時間
    private const float showNoteLocalDistance = 7.7f;

    private AudioSource musicPlayer;
    [SerializeField]
    private AudioClip music;
    [SerializeField]
    private AudioClip hitSound;


    private int currentCombo;
    private int maxCombo;
    private int goodCount;
    private int missCount;
    [SerializeField]
    private Text comboText;
    [SerializeField]
    private Text goodCountText;
    [SerializeField]
    private Text missCountText;


    // Start is called before the first frame update
    void Start()
    {
        musicPlayer = GetComponent<AudioSource>();
        LoadFumen();
        musicPlayer.clip = music;
        musicPlayer.Play();
    }

    // Update is called once per frame
    void Update()
    {
        if (notes.Count != 0) {

            foreach (var key in keys) {
                Note judgedNote = null;
                if (Input.GetKeyDown(key)) {
                    foreach (var note in notes) {
                        if (note.targetKey != key) { continue; }
                        float timeDiffAbs = Mathf.Abs(note.time - musicPlayer.time);
                        if(timeDiffAbs <= judgeRange) {
                            judgedNote = note;
                            break;
                        }
                    }


                    if (judgedNote != null) {
                        JudgeNote(Judge.Good, judgedNote);
                    }
                }
            }



            //ミスの処理
            var missedNotes = new List<Note>();
            foreach(var note in notes) {
                var diffTime = note.time - musicPlayer.time;
                if(diffTime < -judgeRange) {
                    missedNotes.Add(note);
                }
            }

            foreach (var missNote in missedNotes) {
                JudgeNote(Judge.Miss, missNote);
            }



            // ノーツのスクロール処理
            foreach(var note in notes) {
                var diffTime = note.time - musicPlayer.time;
                var noteY = showNoteLocalDistance * (Mathf.Min(diffTime, showNoteLocalDistance) / showNoteTimeRangeSec);

                var noteLocalPosition = note.transform.localPosition;
                noteLocalPosition.y = noteY;
                note.transform.localPosition = noteLocalPosition;
            }

        }
    }

    void LoadFumen() {
        if(fumenFile == null) { return; } //譜面データがInspectorでセットされていないときは何もしない

        Debug.Log(fumenFile.text);

        //TODO: 譜面読み込み
        var lines = fumenFile.text.Split('\n');
        float bpm = 140f;
        float beatTime = 60f / bpm;
        float measureTime = beatTime * 4;
        float measureHeadTime = 0f; // 処理中の小節の0拍目の時間

        // オフセットを設定
        foreach(var line in lines) {
            // 「$」で始まる行だけを処理する
            if (line[0] == '$') {
                //直後の文字列〜「=」までを読む
                var command = ParseCommand(line);
                var commandParam = ParseCommandParam(line);
                //コマンドを判定
                switch (command) {
                    case "Offset":
                        // 「=」以降を数字として読む
                        measureHeadTime = float.Parse(commandParam);
                        break;
                    default:
                        break;
                }
                continue;
            }
        }

        // 譜面データ処理
        foreach (var line in lines) {
            // 各行を読みます

            // なにもない行は読み飛ばす
            if(line.Length == 0) { continue; }
            // 「#」で始まる行は読み飛ばす
            if(line[0] == '#') { continue; }

            // 「$」で始まる行はコマンド行とする
            if(line[0] == '$') {
                // コマンド文字列とパラメーターを解析
                var command = ParseCommand(line);
                var commandParam = ParseCommandParam(line);

                //コマンドを判定
                switch(command) {
                    case "BPM": // BPM変更命令
                        bpm = float.Parse(commandParam);
                        beatTime = 60f / bpm;
                        measureTime = beatTime * 4f;
                        break;
                    default:
                        break;
                }
                continue;
            }

            // 「#」以降を削ぎ落とします

            // それ以外、ノーツ生成
            // コンマの出現回数 + 1 が小説中のノーツの数
            var unitCount = line.CharaCount(',') + 1;
            var unitTime = measureTime / unitCount;
            var noteDataArray = line.Split(',');
            for(var unit = 0; unit < noteDataArray.Length; unit++) {
                var noteList = noteDataArray[unit];
                float noteTime = measureHeadTime + (unitTime * unit);
                for(var i = 0; i < noteList.Length; i++) {
                    Note note = null;
                    switch(noteList[i]) {
                        case '1':
                            note = Instantiate(notePrefab, lanes[0]);
                            note.targetKey = keys[0];
                            break;
                        case '2':
                            note = Instantiate(notePrefab, lanes[1]);
                            note.targetKey = keys[1];
                            break;
                        case '3':
                            note = Instantiate(notePrefab, lanes[2]);
                            note.targetKey = keys[2];
                            break;
                        case '4':
                            note = Instantiate(notePrefab, lanes[3]);
                            note.targetKey = keys[3];
                            break;
                        default:
                            break;
                    }
                    if(note != null) {
                        note.time = noteTime;
                        note.transform.localPosition = new Vector2(0, showNoteLocalDistance);
                        notes.Add(note);
                    }
                }
            }

            measureHeadTime += measureTime;
        }
    }

    void JudgeNote(Judge judge, Note note) {
        switch(judge) {
            case Judge.Good:
                goodCount += 1;
                currentCombo += 1;
                maxCombo = Mathf.Max(currentCombo, maxCombo);
                musicPlayer.PlayOneShot(hitSound);
                break;
            case Judge.Miss:
                missCount += 1;
                currentCombo = 0;
                break;
            default:
                return;
        }

        comboText.text = "Combo: " + currentCombo + "\n" + "MaxCombo: " + maxCombo;
        goodCountText.text = "Good: " + goodCount;
        missCountText.text = "Miss: " + missCount;


        notes.Remove(note);
        Destroy(note.gameObject);
    }

    string ParseCommand(string line) {
        // 「$」を削ぎ落とします
        line = line.Replace("$", "");
        // 「=」までをコマンド文字列として解釈します
        var commandEndPos = line.IndexOf('=');
        var commandStr = line.Remove(commandEndPos);
        return commandStr;
    }

    string ParseCommandParam(string line) {
        // 「$」を削ぎ落とします
        line = line.Replace("$", "");
        // 「=」までをコマンド文字列として解釈します
        var commandEndPos = line.IndexOf('=');
        var commandParam = line.Remove(0, commandEndPos + 1);
        return commandParam;
    }
}
