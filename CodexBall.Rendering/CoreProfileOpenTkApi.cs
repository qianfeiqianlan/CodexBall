using System.Text.RegularExpressions;
using CodexBall.App.Companion.Live2D;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;

namespace CodexBall.Rendering;

/// <summary>Adapts the pinned SDK's desktop GLSL 120 shaders to a Core profile without changing the SDK.</summary>
internal sealed class CoreProfileOpenTkApi(NativeWindow window) : OpenTkApi(window)
{
    public override void ShaderSource(int shader, string source)
    {
        GL.GetShader(shader, ShaderParameter.ShaderType, out var type);
        base.ShaderSource(shader, Translate(source, type == (int)ShaderType.FragmentShader));
    }

    internal static string Translate(string source, bool fragment)
    {
        if (!Regex.IsMatch(source, @"\A\s*#version\s+120\b"))
            throw new NotSupportedException("Core profile bridge expects the pinned adapter's GLSL 120 desktop shaders.");
        source = Regex.Replace(source, @"\A\s*#version\s+120[^\r\n]*\r?\n", "#version 330 core\n" + (fragment ? "out vec4 codexBallFragmentColor;\n" : ""));
        source = Regex.Replace(source, @"\battribute\b", "in");
        source = Regex.Replace(source, @"\bvarying\b", fragment ? "in" : "out");
        source = Regex.Replace(source, @"\btexture2D\b", "texture");
        return Regex.Replace(source, @"\bgl_FragColor\b", "codexBallFragmentColor");
    }
}
