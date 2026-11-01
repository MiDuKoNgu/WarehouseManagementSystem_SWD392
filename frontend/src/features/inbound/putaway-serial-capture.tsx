import { SerialCapture } from './serial-capture';

/** Recognition fills the surrounding Serial input; the operator still confirms the Putaway. */
export function PutawaySerialCapture({ resetKey, disabled, onChoose, onRescan }: {
  resetKey: string; disabled: boolean; onChoose: (value: string) => void; onRescan?: () => void;
}) {
  return <SerialCapture resetKey={resetKey} disabled={disabled} onRescan={onRescan}
    onChoose={value => onChoose(value)} closeCameraOnChoose
    helpText="Quét hoặc đọc ảnh chỉ điền Serial. Kiểm tra kết quả trước khi tra cứu hoặc xác nhận xếp kho."
    cameraResultText="Đã tự điền vào ô Serial. Đối chiếu tem trước khi xác nhận xếp kho." />;
}
