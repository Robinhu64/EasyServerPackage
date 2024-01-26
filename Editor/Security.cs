using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace security
{
    public class SecurityCommen : MonoBehaviour
    {
        public IPEndPoint Ip;
        public string Name;
        public byte[] Key;
        public SecurityCommen(IPEndPoint ip,string name, byte[] key)
        {
            Ip = ip;
            Name = name;
            if (key != null)
            {
                Key = key;
            }
            else
            {
                Key = null;
            }
        }

        public void SetKey(string inputKey)
        {
            Key = Convert.FromBase64String(inputKey); 
        }
        
    }
    public class SecurityFunctions
    {
        /// <summary>
        /// Decoded Receiving of the encoding input stream 
        /// </summary>
        /// <param name="Stream">The stream of the client that needs to receive the secured message.</param>
        /// <param name="Key">The AES-key for AES-GCM</param>
        /// <returns>String representation of the incoming message</returns>
        public static string SecureReceive(Stream stream, byte[] key)
        {
            UTF8Encoding UTF8 = new UTF8Encoding();

            // Size reader
            byte[] sizebuffer = new byte[4];
            stream.Read(sizebuffer, 0, 4);

            int length = BitConverter.ToInt32(sizebuffer, 0);

            // Message reader
            byte[] buffermessage = new byte[length];
            stream.Read(buffermessage, 0, buffermessage.Length);

            // IV reader
            byte[] bufferIV = new byte[12];
            stream.Read(bufferIV, 0, 12);

            // Decryption
            byte[] plainbytes = DecryptAesGcm(buffermessage, key, bufferIV);

            return Encoding.UTF8.GetString(plainbytes);
        }
        /// <summary>
        /// Encoded secure transmisionm 
        /// </summary>
        /// <param name="Stream">The stream of the client that the encrypted message needs to transmitted to.</param>
        /// <param name="Key">The AES-key for AES-GCM</param>
        /// <returns>Decoded Receiving of the encoding input stream.</returns>
        public static void SecureSend(string message, Stream stream, byte[] key)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(message);
            var IV = new byte[12];
            RandomNumberGenerator.Fill(IV);
            byte[] cipherText = EncryptAesGcm(plainBytes, key, IV);
            byte[] length = BitConverter.GetBytes(cipherText.Length);
            stream.Write(length, 0, 4);
            stream.Write(cipherText, 0, cipherText.Length);
            stream.Write(IV, 0, IV.Length);
            stream.Flush();
        }
        /// <summary>
        /// Encrypt message string using AES-GCM
        /// </summary>
        /// <param name="plaintext">Message that wants to be transmitted in string format.</param>
        /// <param name="Key">The AES-key for AES-GCM.</param>
        /// <param name="nonce">The Number only once, this needs to be unique for every transmission.</param>
        /// <returns>byte-aray of the encrypted input plaintext</returns>
        private static byte[] EncryptAesGcm(byte[] plaintext, byte[] key, byte[] nonce)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), 128, nonce);

            cipher.Init(true, parameters);

            byte[] ciphertext = new byte[cipher.GetOutputSize(plaintext.Length)];
            int len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, ciphertext, 0);
            cipher.DoFinal(ciphertext, len);

            return ciphertext;
        }
        /// <summary>
        /// Encrypt message string using AES-GCM
        /// </summary>
        /// <param name="ciphertext">Byte-array of the encrypted text that needs to be decrypted.</param>
        /// <param name="Key">The AES-key for AES-GCM.</param>
        /// <param name="nonce">The Number only once, this needs to be unique for every transmission.</param>
        /// <returns>byte-aray of the decrypted version of the ciphertext</returns>
        private static byte[] DecryptAesGcm(byte[] ciphertext, byte[] key, byte[] nonce)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), 128, nonce);

            cipher.Init(false, parameters);

            byte[] plaintext = new byte[cipher.GetOutputSize(ciphertext.Length)];
            int len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, plaintext, 0);
            cipher.DoFinal(plaintext, len);

            return plaintext;
        }
    }
}

