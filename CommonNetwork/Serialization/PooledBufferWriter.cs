using System;
using System.Buffers;

namespace MyCommonNet.Serialization
{
    /// <summary>
    /// ArrayPool 기반 버퍼 라이터
    /// IBufferWriter&lt;byte&gt; 구현으로 Zero-allocation 직렬화 지원
    /// 사용 후 반드시 Dispose 호출 또는 using 블록 사용
    /// </summary>
    public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[]? _buffer;
        private int _index;
        private bool _disposed;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="initialCapacity">초기 버퍼 크기 (기본값: 4096)</param>
        public PooledBufferWriter(int initialCapacity = 4096)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "초기 버퍼 크기는 0보다 커야 합니다.");
            }

            _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
            _index = 0;
            _disposed = false;
        }

        /// <summary>
        /// 현재까지 쓰여진 데이터의 Span 뷰
        /// </summary>
        public ReadOnlySpan<byte> WrittenSpan
        {
            get
            {
                ThrowIfDisposed();
                return _buffer.AsSpan(0, _index);
            }
        }

        /// <summary>
        /// 현재까지 쓰여진 데이터의 Memory 뷰
        /// </summary>
        public ReadOnlyMemory<byte> WrittenMemory
        {
            get
            {
                ThrowIfDisposed();
                return _buffer.AsMemory(0, _index);
            }
        }

        /// <summary>
        /// 현재까지 쓰여진 바이트 수
        /// </summary>
        public int WrittenCount
        {
            get
            {
                ThrowIfDisposed();
                return _index;
            }
        }

        /// <summary>
        /// 현재 버퍼의 총 용량
        /// </summary>
        public int Capacity
        {
            get
            {
                ThrowIfDisposed();
                return _buffer?.Length ?? 0;
            }
        }

        /// <summary>
        /// 남은 여유 공간
        /// </summary>
        public int FreeCapacity
        {
            get
            {
                ThrowIfDisposed();
                return (_buffer?.Length ?? 0) - _index;
            }
        }

        /// <summary>
        /// 쓰기 커서를 지정된 바이트 수만큼 전진
        /// </summary>
        /// <param name="count">전진할 바이트 수</param>
        public void Advance(int count)
        {
            ThrowIfDisposed();

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "count는 0 이상이어야 합니다.");
            }

            if (_index + count > _buffer!.Length)
            {
                throw new InvalidOperationException("버퍼 범위를 초과하여 Advance할 수 없습니다.");
            }

            _index += count;
        }

        /// <summary>
        /// 쓰기용 Memory 버퍼 획득
        /// </summary>
        /// <param name="sizeHint">필요한 최소 크기 (0이면 기본값 사용)</param>
        /// <returns>쓰기 가능한 Memory</returns>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            ThrowIfDisposed();
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_index);
        }

        /// <summary>
        /// 쓰기용 Span 버퍼 획득
        /// </summary>
        /// <param name="sizeHint">필요한 최소 크기 (0이면 기본값 사용)</param>
        /// <returns>쓰기 가능한 Span</returns>
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            ThrowIfDisposed();
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_index);
        }

        /// <summary>
        /// 버퍼 용량 확보 (필요 시 확장)
        /// </summary>
        /// <param name="sizeHint">필요한 추가 크기</param>
        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint <= 0)
            {
                sizeHint = 256; // 기본 추가 크기
            }

            int currentFree = _buffer!.Length - _index;
            if (currentFree >= sizeHint)
            {
                return; // 충분한 공간 있음
            }

            // 새로운 버퍼 크기 계산 (최소 2배 또는 필요 크기)
            int newSize = Math.Max(_buffer.Length * 2, _index + sizeHint);
            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

            // 기존 데이터 복사
            _buffer.AsSpan(0, _index).CopyTo(newBuffer);

            // 기존 버퍼 반환
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }

        /// <summary>
        /// 쓰기 위치를 처음으로 리셋 (버퍼는 유지)
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();
            _index = 0;
        }

        /// <summary>
        /// 쓰여진 데이터를 새 배열로 복사하여 반환
        /// </summary>
        /// <returns>쓰여진 데이터의 복사본</returns>
        public byte[] ToArray()
        {
            ThrowIfDisposed();
            return WrittenSpan.ToArray();
        }

        /// <summary>
        /// 리소스 해제 (ArrayPool에 버퍼 반환)
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }

            _disposed = true;
        }

        /// <summary>
        /// Dispose 상태 확인
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PooledBufferWriter));
            }
        }
    }
}
