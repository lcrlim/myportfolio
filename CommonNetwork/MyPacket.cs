using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MessagePack;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 헤더 및 본문 컨테이너
    /// 풀링과 Zero-allocation을 위한 설계
    /// 기본 클래스 속성은 헤더 정보로, 직렬화 대상이 아님
    /// IBufferWriter&lt;byte&gt; 구현으로 직렬화 시 Zero-copy 지원
    /// </summary>
    public class MyPacket : IBufferWriter<byte>
    {
        /// <summary>
        /// 패킷 총 길이, 4바이트, Len 항목의 길이도 포함
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public int Len { get; set; }

        /// <summary>
        /// 패킷의 타입, 4바이트
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public int Type { get; set; }

        /// <summary>
        /// 패킷 데이터의 메모리 뷰 (읽기용)
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public ReadOnlyMemory<byte> BodyMemory { get; set; }

        /// <summary>
        /// 쓰기용 바디 버퍼 (직렬화 결과 저장)
        /// ArrayPool에서 빌린 버퍼 또는 고정 버퍼 참조
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public Memory<byte> WriteBuffer { get; set; }

        /// <summary>
        /// 실제 바디 크기 (WriteBuffer 사용 시)
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public int WriteBufferLength { get; set; }

        /// <summary>
        /// ArrayPool에서 렌트한 읽기 버퍼 (내부 전용)
        /// 패킷이 ArrayPool 버퍼를 사용하는 경우, 이 필드가 null이 아니며 Reset 시 자동 반환됩니다.
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        internal byte[]? _rentedBuffer;

        /// <summary>
        /// ArrayPool에서 렌트한 쓰기 버퍼 (내부 전용)
        /// 응답 패킷 직렬화에 사용
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        internal byte[]? _rentedWriteBuffer;

        /// <summary>
        /// 현재 쓰기 버퍼에 쓰여진 바이트 수 (IBufferWriter용)
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        private int _writtenCount;

        /// <summary>
        /// IBufferWriter를 통해 쓰여진 메모리 뷰
        /// </summary>
        [IgnoreMember]
        [JsonIgnore]
        public ReadOnlyMemory<byte> WrittenMemory => _rentedWriteBuffer.AsMemory(0, _writtenCount);

        /// <summary>
        /// Object Pool 리셋용
        /// ArrayPool 버퍼가 있으면 자동으로 반환.
        /// </summary>
        public void Reset()
        {
            Len = 0;
            Type = 0;
            BodyMemory = ReadOnlyMemory<byte>.Empty;
            WriteBuffer = Memory<byte>.Empty;
            WriteBufferLength = 0;
            _writtenCount = 0;

            // 읽기 버퍼 반환
            if (_rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_rentedBuffer);
                _rentedBuffer = null;
            }

            // 쓰기 버퍼 반환
            if (_rentedWriteBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_rentedWriteBuffer);
                _rentedWriteBuffer = null;
            }
        }

        /// <summary>
        /// 쓰기 버퍼 확보 (레거시 지원 또는 수동 할당용)
        /// </summary>
        /// <param name="size">필요한 버퍼 크기</param>
        /// <returns>쓰기 가능한 Span</returns>
        public Span<byte> EnsureWriteBuffer(int size)
        {
            if (_rentedWriteBuffer == null || _rentedWriteBuffer.Length < size)
            {
                // 기존 버퍼 반환
                if (_rentedWriteBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_rentedWriteBuffer);
                }
                // 새 버퍼 할당
                _rentedWriteBuffer = ArrayPool<byte>.Shared.Rent(size);
            }

            // 수동 할당이므로 처음부터 쓴다고 가정
            _writtenCount = 0;
            WriteBuffer = _rentedWriteBuffer.AsMemory(0, size);
            return _rentedWriteBuffer.AsSpan(0, size);
        }

        #region IBufferWriter<byte> Implementation

        /// <summary>
        /// 쓰기 커서를 전진시킵니다.
        /// </summary>
        public void Advance(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            
            _writtenCount += count;
            
            // 기존 필드와의 호환성 유지
            WriteBufferLength = _writtenCount;
        }

        /// <summary>
        /// 쓰기 가능한 Memory를 반환합니다.
        /// </summary>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _rentedWriteBuffer.AsMemory(_writtenCount);
        }

        /// <summary>
        /// 쓰기 가능한 Span을 반환합니다.
        /// </summary>
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _rentedWriteBuffer.AsSpan(_writtenCount);
        }

        /// <summary>
        /// 버퍼 크기를 확인하고 필요 시 확장합니다.
        /// 기존 데이터를 유지하면서 확장합니다.
        /// </summary>
        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint == 0) sizeHint = 256; // 기본 추가 크기

            int currentLength = _rentedWriteBuffer?.Length ?? 0;
            int available = currentLength - _writtenCount;

            if (available < sizeHint)
            {
                // 최소 2배 또는 필요한 크기만큼 확장
                int newSize = Math.Max(currentLength * 2, _writtenCount + sizeHint);
                // 첫 할당인 경우 4096 등 넉넉하게 잡을 수도 있지만 sizeHint 기준
                if (newSize == 0) newSize = Math.Max(sizeHint, 256);

                byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

                if (_rentedWriteBuffer != null)
                {
                    // 기존 데이터 복사
                    Buffer.BlockCopy(_rentedWriteBuffer, 0, newBuffer, 0, _writtenCount);
                    ArrayPool<byte>.Shared.Return(_rentedWriteBuffer);
                }

                _rentedWriteBuffer = newBuffer;
                
                // WriteBuffer 뷰 업데이트 (전체 영역 참조)
                WriteBuffer = _rentedWriteBuffer.AsMemory();
            }
        }

        #endregion
    }
}
