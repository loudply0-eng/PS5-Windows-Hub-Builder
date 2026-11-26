"""Strict DER validation for an uncompressed P-256 SubjectPublicKeyInfo."""

EC_PUBLIC_KEY_OID = bytes.fromhex("2a8648ce3d0201")
PRIME256V1_OID = bytes.fromhex("2a8648ce3d030107")


def _read_tlv(data: bytes, offset: int, expected_tag: int) -> tuple[bytes, int]:
    if offset >= len(data) or data[offset] != expected_tag:
        raise ValueError("Public key is not a canonical P-256 SubjectPublicKeyInfo.")

    offset += 1
    if offset >= len(data):
        raise ValueError("Truncated DER length in public key.")

    first_length = data[offset]
    offset += 1
    if first_length < 0x80:
        length = first_length
    else:
        count = first_length & 0x7F
        if count == 0 or count > 4 or offset + count > len(data):
            raise ValueError("Invalid DER length in public key.")
        if data[offset] == 0:
            raise ValueError("Non-minimal DER length in public key.")
        length = int.from_bytes(data[offset:offset + count], "big")
        if length < 0x80 or (count > 1 and length < (1 << (8 * (count - 1)))):
            raise ValueError("Non-minimal DER length in public key.")
        offset += count

    end = offset + length
    if end > len(data):
        raise ValueError("Truncated DER value in public key.")
    return data[offset:end], end


def validate_p256_spki(der: bytes) -> None:
    """Reject non-P-256 or malformed SPKI DER before passing it to OpenSSL."""
    if not isinstance(der, bytes) or not der or len(der) > 256:
        raise ValueError("Public key DER is empty or exceeds the supported size.")

    sequence, end = _read_tlv(der, 0, 0x30)
    if end != len(der):
        raise ValueError("Trailing data after public-key SubjectPublicKeyInfo.")

    algorithm, position = _read_tlv(sequence, 0, 0x30)
    algorithm_oid, algorithm_position = _read_tlv(algorithm, 0, 0x06)
    curve_oid, algorithm_end = _read_tlv(algorithm, algorithm_position, 0x06)
    if algorithm_end != len(algorithm) or algorithm_oid != EC_PUBLIC_KEY_OID or curve_oid != PRIME256V1_OID:
        raise ValueError("The supplied public key is not an ECDSA P-256 key.")

    public_bits, position = _read_tlv(sequence, position, 0x03)
    if position != len(sequence) or len(public_bits) != 66 or public_bits[0] != 0 or public_bits[1] != 0x04:
        raise ValueError("P-256 public key must contain one uncompressed 65-byte EC point.")
