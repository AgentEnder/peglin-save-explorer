import React, { useCallback, useState } from "react";
import {
  Typography,
  Box,
  Button,
  Alert,
  CircularProgress,
} from "@mui/material";
import { Upload as UploadIcon } from "@mui/icons-material";
import { useAppActions } from "../store/useAppStore";

interface FileUploadProps {
  onUploadComplete?: () => void;
}

const FileUpload: React.FC<FileUploadProps> = ({ onUploadComplete }) => {
  const { uploadSaveFile } = useAppActions();
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const handleFileUpload = useCallback(
    async (file: File) => {
      if (!file) return;

      setUploading(true);
      setError(null);
      setSuccess(null);

      try {
        await uploadSaveFile(file);
        setSuccess(`Successfully loaded save file: ${file.name}`);
        if (onUploadComplete) {
          onUploadComplete();
        }
      } catch (err) {
        const errorMessage =
          err instanceof Error ? err.message : "Failed to upload file";
        setError(errorMessage);
      } finally {
        setUploading(false);
      }
    },
    [uploadSaveFile, onUploadComplete]
  );

  const handleFileSelect = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (file) {
        handleFileUpload(file);
      }
    },
    [handleFileUpload]
  );

  const handleDrop = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      event.preventDefault();
      const file = event.dataTransfer.files[0];
      if (file) {
        handleFileUpload(file);
      }
    },
    [handleFileUpload]
  );

  const handleDragOver = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      event.preventDefault();
    },
    []
  );

  const handleDragLeave = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      event.preventDefault();
    },
    []
  );

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Upload Save File
      </Typography>

      <Alert severity="info" sx={{ mb: 3 }}>
        Upload your Peglin save file to analyze your run history. Supported
        formats: .data files
      </Alert>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      <Box
        sx={{
          p: 4,
          border: "2px dashed",
          borderColor: uploading ? "primary.main" : "grey.400",
          borderRadius: 2,
          textAlign: "center",
          cursor: uploading ? "default" : "pointer",
          backgroundColor: uploading ? "action.hover" : "background.paper",
          "&:hover": {
            borderColor: "primary.main",
            backgroundColor: "action.hover",
          },
        }}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
      >
        <input
          type="file"
          accept=".data,.save"
          style={{ display: "none" }}
          onChange={handleFileSelect}
          disabled={uploading}
          id="file-upload-input"
        />

        {uploading ? (
          <>
            <CircularProgress sx={{ mb: 2 }} />
            <Typography variant="h6" gutterBottom>
              Uploading and processing...
            </Typography>
          </>
        ) : (
          <>
            <UploadIcon sx={{ fontSize: 64, color: "grey.400", mb: 2 }} />

            <Typography variant="h6" gutterBottom>
              Drag and drop your save file here
            </Typography>

            <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
              or
            </Typography>

            <Button
              variant="contained"
              component="label"
              htmlFor="file-upload-input"
            >
              Browse Files
            </Button>
          </>
        )}

        <Typography variant="caption" display="block" sx={{ mt: 2 }}>
          Supported file types: .data, .save
        </Typography>
      </Box>

      <Box sx={{ mt: 3 }}>
        <Typography variant="h6" gutterBottom>
          How to find your save files:
        </Typography>
        <Typography variant="body2" component="div">
          <ul>
            <li>
              <strong>Windows:</strong> %USERPROFILE%\AppData\LocalLow\Red Nexus
              Games\Peglin
            </li>
            <li>
              <strong>Mac:</strong> ~/Library/Application Support/Red Nexus
              Games/Peglin
            </li>
            <li>
              <strong>Linux:</strong> ~/.config/unity3d/Red Nexus Games/Peglin
            </li>
          </ul>
        </Typography>
        <Typography variant="body2" sx={{ mt: 1 }}>
          Look for files like "Save_0.data" or "Stats_0.data"
        </Typography>
      </Box>
    </Box>
  );
};

export default FileUpload;
