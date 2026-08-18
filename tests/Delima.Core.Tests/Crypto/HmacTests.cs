using System.Text;
using Delima.Core.Crypto;
using Xunit;

namespace Delima.Core.Tests.Crypto;

public class HmacTests
{
    [Fact]
    public void HmacSha256_KnownAnswerTest_Rfc4231TestCase1()
    {
        // RFC 4231 Test Case 1
        // Key = 20 bytes of 0x0b
        // Data = "Hi There"
        // Digest = b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7
        byte[] key = new byte[20];
        Array.Fill(key, (byte)0x0b);
        byte[] data = Encoding.ASCII.GetBytes("Hi There");
        byte[] expected = Convert.FromHexString("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7");

        byte[] actual = HmacHelper.ComputeHmac(key, data);

        Assert.Equal(expected, actual);
        Assert.True(HmacHelper.VerifyHmac(key, data, expected));
    }

    [Fact]
    public void HmacSha256_VerifyHmac_ReturnsFalseOnTamperedData()
    {
        byte[] key = Encoding.UTF8.GetBytes("test-key-32-bytes-long-for-hmac!");
        byte[] data = Encoding.UTF8.GetBytes("original data");
        byte[] validHmac = HmacHelper.ComputeHmac(key, data);

        byte[] tamperedData = Encoding.UTF8.GetBytes("tampered data");
        Assert.False(HmacHelper.VerifyHmac(key, tamperedData, validHmac));

        byte[] tamperedHmac = (byte[])validHmac.Clone();
        tamperedHmac[0] ^= 0xFF;
        Assert.False(HmacHelper.VerifyHmac(key, data, tamperedHmac));
    }
}
