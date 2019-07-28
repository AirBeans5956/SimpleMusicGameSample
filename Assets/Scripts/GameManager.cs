using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音ゲー進行を管理するComponent
/// </summary>
public class GameManager : MonoBehaviour
{
    // --- 定数系 ---
    /// <summary>
    /// 判定の種類
    /// </summary>
    enum Judge { Good, Miss };

    /// <summary>
    /// 判定に用いるキーの一覧
    /// </summary>
    private readonly KeyCode[] keys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };

    /// <summary>
    /// Good判定の範囲(秒)
    /// </summary>
    private const float judgeRange = 0.1f;

    /// <summary>
    /// ノーツが画面に見えている時間(秒)
    /// </summary>
    private const float showNoteTimeRangeSec = 0.75f;

    /// <summary>
    /// ↑のノーツ表示時間でノーツが降ってくる距離(Y方向)
    /// </summary>
    private const float showNoteLocalDistance = 7.7f;

    // --- エディタからセットする諸々 ---
    /// <summary>
    /// レーン群
    /// </summary>
    [SerializeField]
    private Transform[] lanes;

    /// <summary>
    /// ノーツのPrefab
    /// </summary>
    [SerializeField]
    private Note notePrefab;

    /// <summary>
    /// 音ゲーに用いる音源
    /// </summary>
    [SerializeField]
    private AudioClip music;

    /// <summary>
    /// 成功判定時の効果音
    /// </summary>
    [SerializeField]
    private AudioClip hitSound;

    /// <summary>
    /// 譜面のテキストファイル
    /// </summary>
    public TextAsset fumenFile;

    // --- Componentのキャッシュ ---
    /// <summary>
    /// 音楽再生用のAudioSource
    /// </summary>
    private AudioSource musicPlayer;

    /// <summary>
    /// 生成したノーツをしまっておく配列
    /// </summary>
    private List<Note> notes = new List<Note>();

    // --- UI系 ---
    /// <summary>
    /// コンボ表示
    /// </summary>
    [SerializeField]
    private Text comboText;

    /// <summary>
    /// 成功判定数表示
    /// </summary>
    [SerializeField]
    private Text goodCountText;

    /// <summary>
    /// ミス判定数表示
    /// </summary>
    [SerializeField]
    private Text missCountText;

    // --- スコア系変数 ---
    /// <summary>
    /// 現在のコンボ(連続成功判定数)数
    /// </summary>
    private int currentCombo;

    /// <summary>
    /// 最大コンボ数
    /// </summary>
    private int maxCombo;

    /// <summary>
    /// 成功判定数
    /// </summary>
    private int goodCount;

    /// <summary>
    /// ミス判定数
    /// </summary>
    private int missCount;


    // Start is called before the first frame update
    void Start()
    {
        musicPlayer = GetComponent<AudioSource>(); // 音楽再生用Componentを取得
        musicPlayer.clip = music; // 音楽を再生用Componentにセット

        LoadFumen(); // 譜面データ読み込み

        musicPlayer.Play(); // 音楽を再生、ゲーム開始
    }

    // Update is called once per frame
    void Update()
    {
        // ノーツが残っているときだけノーツ処理
        if (notes.Count != 0) {
            // 入力処理
            foreach (var key in keys) {
                Note judgedNote = null; // 判定対象のノーツが代入される
                if (Input.GetKeyDown(key)) { // そもそもキーが押されてなければ処理しない
                    foreach (var note in notes) { // すべてのノーツをチェック
                        if (note.targetKey != key) { continue; } // レーンが違うノーツは処理しない
                        float timeDiffAbs = Mathf.Abs(note.time - musicPlayer.time); // 曲の再生位置とノーツの時間の差分を絶対値で取る
                        if(timeDiffAbs <= judgeRange) { // ノーツが判定時間内なら成功!
                            judgedNote = note;
                            break; // 判定に成功したので他のノーツは処理しない
                        }
                    } // 全ノーツチェックここまで

                    // 判定に成功したノーツがあれば、成功判定処理を呼び出す
                    if (judgedNote != null) {
                        JudgeNote(Judge.Good, judgedNote);
                    }
                }
            }


            // ミスの処理
            var missedNotes = new List<Note>();
            foreach(var note in notes) {
                var diffTime = note.time - musicPlayer.time; // 曲の再生位置とノーツの時間の差分を取る
                if(diffTime < -judgeRange) { // ノーツが判定時間を過ぎていたらミス判定の対象にする
                    missedNotes.Add(note);
                }
            }
            // ミス判定になったノーツを処理する
            foreach (var missNote in missedNotes) {
                JudgeNote(Judge.Miss, missNote);
            }


            // 成功・ミス判定どちらもされず、残ったノーツのスクロール処理
            foreach(var note in notes) {
                var diffTime = note.time - musicPlayer.time; // 曲とノーツの時間の差分を取る
                // 差分の時間を、ノーツ表示時間割って、ノーツの表示高さで掛ける
                var noteY = showNoteLocalDistance * (Mathf.Min(diffTime, showNoteLocalDistance) / showNoteTimeRangeSec);

                // ノーツのY座標を更新
                var noteLocalPosition = note.transform.localPosition;
                noteLocalPosition.y = noteY;
                note.transform.localPosition = noteLocalPosition;
            }

        }
    }

    /// <summary>
    /// 譜面データを読み込んで、ノーツを生成します
    /// </summary>
    void LoadFumen() {
        if(fumenFile == null) { return; } //譜面データがInspectorでセットされていないときは何もしない

        var lines = fumenFile.text.Split('\n'); // 譜面データを取り出し、改行で区切って配列化

        // --- 時間計算用の変数初期化 ---
        float bpm = 120f; // BPM、デフォルト値は120BPM
        float beatTime = 60f / bpm; // 1拍の時間(秒)
        float measureTime = beatTime * 4; // 1小節の時間(秒、このゲームでは4分の4拍子固定)
        float measureHeadTime = 0f; // 処理中の小節の、最初の拍の時間

        // 譜面データの全行を読んで、オフセット設定を読み込み
        foreach(var line in lines) {
            if (line[0] == '$') { // 行の1文字目が「$」か?
                //コマンドとパラメーター取り出し
                var command = ParseCommand(line);
                var commandParam = ParseCommandParam(line);
                // オフセット設定コマンドか?
                if (command == "Offset") {
                    measureHeadTime = float.Parse(commandParam); // 曲の開始時間を変更
                }
                continue;
            }
        }

        // 譜面データ処理
        foreach (var line in lines) { // 譜面データの全部の行を読み込み
            // なにもない行は読み飛ばす
            if(line.Length == 0) { continue; }
            // 「#」で始まる行は読み飛ばす
            if(line[0] == '#') { continue; }

            if(line[0] == '$') { // 行が「$」で始まっているか?
                // コマンド文字列とパラメーターを取得
                var command = ParseCommand(line);
                var commandParam = ParseCommandParam(line);

                //コマンドを判定
                switch(command) {
                    case "BPM": // BPM変更命令なら、小節あたりの時間が変わるので再計算
                        bpm = float.Parse(commandParam);
                        beatTime = 60f / bpm;
                        measureTime = beatTime * 4f;
                        break;
                    default:
                        break;
                }
                continue;
            }

            // それ以外、ノーツ生成
            var unitCount = line.CharaCount(',') + 1; // コンマの出現回数 + 1 が小節中のノーツの数
            var unitTime = measureTime / unitCount; // 処理する小節中の、1拍の時間
            var noteDataArray = line.Split(','); // コンマで分割、拍ごとに区切る
            for(var unit = 0; unit < noteDataArray.Length; unit++) { // 各拍を処理
                var noteList = noteDataArray[unit]; // この拍に出現するノーツを取得
                float noteTime = measureHeadTime + (unitTime * unit); // この拍の時間を算出(小節の始まりの時間+拍あたりの時間×今の小節内の拍数)
                for(var i = 0; i < noteList.Length; i++) { // 現在の拍に出現するノーツを処理
                    Note note = null;
                    switch(noteList[i]) { // 数字を読み、ノーツ生成
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
                    // ノーツが生成されたら、ノーツ配列に追加しておき、Y座標を初期化
                    if(note != null) {
                        note.time = noteTime;
                        note.transform.localPosition = new Vector2(0, showNoteLocalDistance);
                        notes.Add(note);
                    }
                }
            }

            // 次の小節の始まる時間を計算
            measureHeadTime += measureTime;
        }
    }

    /// <summary>
    /// ノートが判定されるときに呼び出されます
    /// </summary>
    /// <param name="judge">どの判定か</param>
    /// <param name="note">判定されたノート</param>
    void JudgeNote(Judge judge, Note note) {
        switch(judge) {
            case Judge.Good: // 成功判定の処理
                // 成功判定数を増やし、コンボ数を+1
                goodCount += 1;
                currentCombo += 1;
                maxCombo = Mathf.Max(currentCombo, maxCombo); // 最大コンボ数が更新されていれば、最大コンボ数を更新
                musicPlayer.PlayOneShot(hitSound); // 成功判定音を再生
                break;
            case Judge.Miss: // ミス判定の処理
                // ミス判定数を+1し、コンボを終了
                missCount += 1;
                currentCombo = 0;
                break;
            default:
                return;
        }

        // UIを更新
        comboText.text = "Combo: " + currentCombo + "\n" + "MaxCombo: " + maxCombo; // コンボ数更新
        goodCountText.text = "Good: " + goodCount; // 成功判定数更新
        missCountText.text = "Miss: " + missCount; // ミス判定数更新

        // 判定処理されたノートの削除
        notes.Remove(note);
        Destroy(note.gameObject);
    }

    /// <summary>
    /// コマンド行から、コマンド名だけを取り出します
    /// </summary>
    /// <param name="line">コマンド行の文字列</param>
    /// <returns>コマンド名の文字列</returns>
    string ParseCommand(string line) {
        // 「$」を削ぎ落とします
        line = line.Replace("$", "");
        // 「=」までをコマンド文字列として解釈し取り出して呼び出し元に返します
        var commandEndPos = line.IndexOf('=');
        var commandStr = line.Remove(commandEndPos);
        return commandStr;
    }

    /// <summary>
    /// コマンド行から、コマンドのパラメーターだけを取り出します
    /// </summary>
    /// <param name="line">コマンドのパラメーター</param>
    /// <returns>コマンドのパラメーターの文字列</returns>
    string ParseCommandParam(string line) {
        // 「$」を削ぎ落とします
        line = line.Replace("$", "");
        // 「=」以降をコマンドパラメーターとして解釈して、取り出し、呼び出し元に返します
        var commandEndPos = line.IndexOf('=');
        var commandParam = line.Remove(0, commandEndPos + 1);
        return commandParam;
    }
}
