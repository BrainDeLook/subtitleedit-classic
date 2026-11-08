using System.Linq;
using Nikse.SubtitleEdit.Logic.SpeechToText;
namespace Classic54Tests
{
 public class SpeechVad54Tests
 {
  [Fact]public void GraniteGetsSileroByDefault()
  {
   var engine=new SpeechEngine{Name="Granite",Family="crisp",Backend="granite-4.1"};var settings=new ClassicSpeechOptions();Assert.True(ClassicSpeechExtras.UseVad(engine,settings));Assert.Equal("silero",ClassicSpeechExtras.EffectiveVad(engine,settings));
  }
  [Theory][InlineData("--chunk-seconds 25")][InlineData("-ck 15")][InlineData("--vad-model custom.gguf")][InlineData("--vad")][InlineData("-vm custom.gguf")]
  public void ExplicitChunkingOrVadIsNotOverridden(string arguments)
  {
   var engine=new SpeechEngine{Name="Granite",Family="crisp",Backend="granite-4.1"};var settings=new ClassicSpeechOptions();settings.ExtraArguments[engine.Name]=arguments;Assert.False(ClassicSpeechExtras.UseVad(engine,settings));
  }
  [Fact]public void OtherBackendsRetainTheirVadChoice()
  {
   var engine=new SpeechEngine{Name="Parakeet",Family="crisp",Backend="parakeet"};var settings=new ClassicSpeechOptions();Assert.False(ClassicSpeechExtras.UseVad(engine,settings));settings.UseVad=true;settings.Vad="firered";Assert.True(ClassicSpeechExtras.UseVad(engine,settings));Assert.Equal("firered",ClassicSpeechExtras.EffectiveVad(engine,settings));
  }
 }
}
