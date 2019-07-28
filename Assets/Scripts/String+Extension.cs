/// <summary>
/// String型にメソッド(のようなもの)を追加します
/// (「拡張メソッド」という機能を利用しています)
/// </summary>
static class StringExtension {
    /// <summary>
    /// 指定の文字の出現回数をカウントします
    /// </summary>
    /// <param name="s">対象の文字列</param>
    /// <param name="c">カウントする文字</param>
    /// <returns></returns>
    public static int CharaCount (this string s, char c) {
        return s.Length - (s.Replace(c.ToString(), "")).Length;
    }
}