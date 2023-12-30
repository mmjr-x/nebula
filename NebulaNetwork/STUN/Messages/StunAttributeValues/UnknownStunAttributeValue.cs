﻿using Microsoft;
using STUN.Enums;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Validation;

namespace STUN.Messages.StunAttributeValues;

/// <summary>
/// https://tools.ietf.org/html/rfc5389#section-15.9
/// </summary>
public class UnknownStunAttributeValue : IStunAttributeValue
{
	public List<AttributeType> Types { get; } = new();

	public int WriteTo(Span<byte> buffer)
	{
		int size = Types.Count << 1;
		Requires.Range(buffer.Length >= size, nameof(buffer));

		foreach (AttributeType attributeType in Types)
		{
			BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)attributeType);
			buffer = buffer[sizeof(ushort)..];
		}

		return size;
	}

	public bool TryParse(ReadOnlySpan<byte> buffer)
	{
		if (buffer.Length < 2 || (buffer.Length & 1) == 1)
		{
			return false;
		}

		Types.Clear();
		while (!buffer.IsEmpty)
		{
			ushort type = BinaryPrimitives.ReadUInt16BigEndian(buffer);
			Types.Add((AttributeType)type);
			buffer = buffer[sizeof(ushort)..];
		}

		return true;
	}
}
