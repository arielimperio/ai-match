import { Component, forwardRef, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-rich-text-editor',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './rich-text-editor.component.html',
  styleUrls: ['./rich-text-editor.component.css'],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => RichTextEditorComponent),
      multi: true
    }
  ]
})
export class RichTextEditorComponent implements ControlValueAccessor, AfterViewInit {
  @ViewChild('editor') editor!: ElementRef;

  content: string = '';
  isDisabled: boolean = false;

  onChange: (value: string) => void = () => { };
  onTouched: () => void = () => { };

  ngAfterViewInit() {
    if (this.content) {
      this.editor.nativeElement.innerHTML = this.content;
    }
  }

  // Formatting commands
  format(command: string, value: string | undefined = undefined) {
    document.execCommand(command, false, value);
    this.updateModel();
    this.editor.nativeElement.focus();
  }

  insertLink() {
    const url = prompt('Enter link URL:');
    if (url) {
      this.format('createLink', url);
    }
  }

  // Handle content changes
  onInput() {
    this.updateModel();
  }

  updateModel() {
    const html = this.editor.nativeElement.innerHTML;
    this.content = html;
    this.onChange(html);
  }

  // ControlValueAccessor implementation
  writeValue(value: string): void {
    this.content = value || '';
    if (this.editor && this.editor.nativeElement) {
      this.editor.nativeElement.innerHTML = this.content;
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
    if (this.editor) {
      this.editor.nativeElement.contentEditable = isDisabled ? 'false' : 'true';
    }
  }
}
