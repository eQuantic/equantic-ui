import { $eq, Box, BoxStyle, BuildContext, Button, Column, CornerRadii, DropTarget, EdgeInsets, Flexible, LiveRegion, PickedFile, ProgressBar, Row, SdkStrings, SizeValue, StatefulComponent, Text, UiComponent, Upload, UploadedFile, UploadPolicy, UploadStateValue } from "../runtime-exports";

export class FileUpload extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.FileUpload';
    _config: FileUpload;
    _uploads: Upload[] = [];
    _subscriptions: any[] = [];
    _reported: Upload[] = [];
    _over: boolean = false;
    _announcement: string = '';
    declare policy: UploadPolicy;
    declare onCompleted: (uploadedFile: UploadedFile) => void;
    declare multiple: boolean;
    declare label: any;
    declare hint: any;

    constructor(policy?: any, onCompleted?: any, multiple: any = true, label: any = null, hint: any = null, props?: any) {
        super();
        if (policy !== undefined) this.policy = policy;
        if (onCompleted !== undefined) this.onCompleted = onCompleted;
        if (multiple !== undefined) this.multiple = multiple;
        if (label !== undefined) this.label = label;
        if (hint !== undefined) this.hint = hint;
        if (this.multiple === undefined) this.multiple = false;
        this.policy = policy;
        this.onCompleted = onCompleted;
        this.multiple = multiple;
        this.label = label;
        this.hint = hint;
        this._config = this;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let config = this._config;
        let picker = context.getService('IFilePicker');
        let uploads = context.getService('IUploads');
        let enabled = (picker != null && picker.isAvailable === true) && (uploads != null && uploads.isAvailable === true);
        let invitation = new Column(8, 'start', 'center');
        invitation.add(new Text(config.label ?? (config.multiple ? SdkStrings.uploadDropHere : SdkStrings.uploadDropOneHere), 'label', theme.textPrimary, 2, 'center'));
        invitation.add(new Text(config.hint ?? FileUpload.accepted(config.policy), 'caption', theme.textSecondary, 2, 'center'));
        invitation.add(new Button(config.multiple ? SdkStrings.uploadChooseFiles : SdkStrings.uploadChooseFile, 'secondary', 'small', () => this.choose(picker, uploads), { disabled: !enabled }));
        let area = new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.all(24), background: this._over && enabled ? theme.colors('primary').subtle : theme.surfaceSubtle, borderWidth: this._over && enabled ? 2 : 1, borderColor: this._over && enabled ? theme.colors('primary').base : theme.borderStrong, cornerRadius: new CornerRadii(theme.shape('large')), opacity: enabled ? null : Math.fround(0.6) }), invitation);
        let column = new Column(12, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        column.add(new DropTarget(area, (files) => this.send(uploads, files), { onDragOverChanged: (over: boolean) => this.setState(() => this._over = over), enabled: enabled }));
        for (const upload of this._uploads) column.add(this.rowOf(upload, theme));
        column.add(new LiveRegion(new Text(this._announcement, 'caption', theme.textSecondary, 3)));
        return column;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if ((next instanceof FileUpload && (fresh = next, true))) this._config = fresh;
    }

    onUnmount() {
        for (const subscription of this._subscriptions) subscription.dispose();
        this._subscriptions.splice(0);
        for (const upload of this._uploads) upload.pause();
    }

    rowOf(upload: Upload, theme: any) {
        let state: UploadStateValue = upload.state;
        let failed = state === 'failed';
        let heading = new Row(8, 'start', 'center', false, null, null, { width: SizeValue.fill });
        heading.add(new Flexible(new Text(upload.file.name, 'label', theme.textPrimary, 1)));
        heading.add(new Text(FileUpload.statusOf(upload), 'caption', failed ? theme.colors('destructive').base : theme.textSecondary, 1));
        let row = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        row.add(heading);
        row.add(new ProgressBar(FileUpload.fraction(upload), failed ? 'destructive' : state === 'completed' ? 'success' : 'primary', { label: SdkStrings.uploadProgress(upload.file.name) }));
        let error: any; 
        if (failed && (($0) => (($0 != null && $0.length > 0) && (error = $0, true)))(upload.error)) row.add(new Text(error, 'caption', theme.colors('destructive').base, 3));
        let actions = new Row(8);
        if (state === 'waiting' || state === 'sending') actions.add(new Button(SdkStrings.uploadPause, 'ghost', 'small', upload.pause.bind(upload)));
        if (state === 'paused') actions.add(new Button(SdkStrings.uploadResume, 'ghost', 'small', upload.resume.bind(upload)));
        if (state === 'waiting' || state === 'sending' || state === 'paused') actions.add(new Button(SdkStrings.uploadCancel, 'ghost', 'small', upload.cancel.bind(upload)));
        row.add(actions);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.all(12), background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('medium')) }), row);
    }

    async choose(picker: any, uploads: any) {
        if (picker == null) return;
        let filters = this._config.policy.filters;
        if (this._config.multiple) {
            this.send(uploads, await picker.pickFilesAsync(filters));
            return;
        }
        let file = await picker.pickFileAsync(filters);
        if (!(file == null)) this.send(uploads, [file]);
    }

    send(uploads: any, files: PickedFile[]) {
        if (uploads == null || $eq.collections.count(files) === 0) return;
        let config = this._config;
        let count = config.multiple ? $eq.collections.count(files) : 1;
        this.setState(() => {
            for (let i = 0; i < count; i++) {
                let upload = uploads.start($eq.collections.item(files, i), config.policy);
                this._uploads.push(upload);
                this._subscriptions.push(upload.subscribe(this.changed.bind(this)));
                if (upload.state === 'failed') this._announcement = FileUpload.announcement(upload);
            }
        });
    }

    changed(upload: Upload) {
        let arrived = upload.state === 'completed' && !$eq.collections.contains(this._reported, upload, 'item');
        this.setState(() => {
            if (arrived) {
                this._reported.push(upload);
                this._announcement = FileUpload.announcement(upload);
            } else if (upload.state === 'failed') {
                this._announcement = FileUpload.announcement(upload);
            }
        });
        let result: any; 
        if (arrived && (result = upload.result) != null) this._config.onCompleted(result);
    }

    static announcement(upload: Upload) {
        return upload.state === 'completed' ? SdkStrings.uploadAnnounceCompleted(upload.file.name) : SdkStrings.uploadAnnounceFailed(upload.file.name, upload.error ?? '');
    }

    static statusOf(upload: Upload) {
        return (() => { const $s = upload.state; if ($s === 'waiting') return SdkStrings.uploadWaiting; if ($s === 'sending') return SdkStrings.uploadSending(FileUpload.sizeText(upload.sent), FileUpload.sizeText(upload.size)); if ($s === 'paused') return SdkStrings.uploadPaused; if ($s === 'completed') return SdkStrings.uploadCompleted; if ($s === 'cancelled') return SdkStrings.uploadCancelled; return ''; })();
    }

    static fraction(upload: Upload) {
        return upload.size === 0n ? 1 : Math.fround((Number(upload.sent) / Number(upload.size)));
    }

    static accepted(policy: UploadPolicy) {
        let formats: string[] = [];
        for (const filter of policy.filters) {
            for (const extension of filter.extensions) {
                let format = $eq.text.toUpper($eq.text.trimStart(extension, '.'), true);
                if (format.length > 0 && !formats.includes(format)) formats.push(format);
            }
        }
        let size = FileUpload.sizeText(policy.maxSize);
        return formats.length === 0 ? SdkStrings.uploadAcceptsAny(size) : SdkStrings.uploadAccepts($eq.text.join(', ', formats), size);
    }

    static sizeText(bytes: bigint) {
        if (bytes < 1024n) return $eq.text.format(bytes, null) + ' B';
        let value = Number(bytes) / 1024.0;
        let unit = 'KB';
        if (value >= 1024) {
            value /= 1024;
            unit = 'MB';
        }
        if (value >= 1024) {
            value /= 1024;
            unit = 'GB';
        }
        return $eq.text.format(value, '0.#', undefined, undefined, 'double') + ' ' + unit;
    }
}

