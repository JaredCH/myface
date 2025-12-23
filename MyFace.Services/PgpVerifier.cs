using System;
using System.IO;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Utilities.IO;

namespace MyFace.Services;

public static class PgpVerifier
{
    public static bool TryGetPrimaryPublicKey(string armoredPublicKey, out PgpPublicKey? publicKey, out string? fingerprintHex, out string? error)
    {
        publicKey = null;
        fingerprintHex = null;
        error = null;
        try
        {
            using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey));
            using var decoded = PgpUtilities.GetDecoderStream(keyStream);
            var keyRingBundle = new PgpPublicKeyRingBundle(decoded);
            foreach (PgpPublicKeyRing ring in keyRingBundle.GetKeyRings())
            {
                var key = ring.GetPublicKeys().Cast<PgpPublicKey>().FirstOrDefault(k => k.IsEncryptionKey || k.IsMasterKey);
                if (key != null)
                {
                    publicKey = key;
                    fingerprintHex = BitConverter.ToString(key.GetFingerprint()).Replace("-", string.Empty).ToUpperInvariant();
                    return true;
                }
            }
            error = "No public key found in bundle.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Failed to parse public key: {ex.Message}";
            return false;
        }
    }

    public static bool VerifySignature(string armoredPublicKey, string armoredSignature, string message, out string? error)
    {
        error = null;
        try
        {
            if (!TryGetPrimaryPublicKey(armoredPublicKey, out var pubKey, out _, out var keyErr) || pubKey == null)
            {
                error = keyErr ?? "No public key";
                return false;
            }

            using var sigStream = new MemoryStream(Encoding.UTF8.GetBytes(armoredSignature));
            using var sigDecoded = PgpUtilities.GetDecoderStream(sigStream);
            var pgpObjFactory = new PgpObjectFactory(sigDecoded);
            PgpSignatureList? sigList = null;

            object? obj;
            while ((obj = pgpObjFactory.NextPgpObject()) != null)
            {
                if (obj is PgpSignatureList list)
                {
                    sigList = list;
                    break;
                }
                else if (obj is PgpSignature sig)
                {
                    sigList = new PgpSignatureList(new[] { sig });
                    break;
                }
            }

            if (sigList == null || sigList.Count < 1)
            {
                error = "No signature found";
                return false;
            }

            var signature = sigList[0];
            signature.InitVerify(pubKey);
            var msgBytes = Encoding.UTF8.GetBytes(message);
            signature.Update(msgBytes);

            var verified = signature.Verify();
            if (!verified)
            {
                error = "Signature verification failed";
            }
            return verified;
        }
        catch (Exception ex)
        {
            error = $"Verification error: {ex.Message}";
            return false;
        }
    }
}
