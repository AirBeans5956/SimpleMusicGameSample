using System.Collections;
using System.Collections.Generic;

static class StringExtension {
    public static int CharaCount (this string s, char c) {
		return s.Length - (s.Replace(c.ToString(), "")).Length;
    }
}